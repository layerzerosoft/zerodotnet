using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.RabbitMq.Configuration;
using LayerZero.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LayerZero.Messaging.RabbitMq;

internal sealed class RabbitMqConsumerHostedService(
    string name,
    RabbitMqConnectionProvider connectionProvider,
    IMessageTopologyManifest topologyManifest,
    IMessageRouteResolver routeResolver,
    IMessageConventions conventions,
    IOptions<MessagingOptions> messagingOptions,
    IOptionsMonitor<RabbitMqBusOptions> busOptions,
    IServiceScopeFactory scopeFactory,
    IMessageRegistry registry,
    MessageEnvelopeSerializer serializer,
    IEnumerable<IMessageSettlementObserver> observers) : BackgroundService
{
    private readonly List<IChannel> consumerChannels = [];
    private readonly Lock consumerChannelsGate = new();
    private readonly IMessageSettlementObserver[] observers = observers.ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!busOptions.Get(name).EnableConsumers)
        {
            return;
        }

        foreach (var binding in GetBindings())
        {
            var channel = await connectionProvider.CreateChannelAsync(stoppingToken).ConfigureAwait(false);
            lock (consumerChannelsGate)
            {
                consumerChannels.Add(channel);
            }

            await channel.BasicQosAsync(0, busOptions.Get(name).PrefetchCount, global: false, cancellationToken: stoppingToken).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, eventArgs) =>
            {
                await HandleDeliveryAsync(channel, binding, eventArgs, stoppingToken).ConfigureAwait(false);
            };

            await channel.BasicConsumeAsync(binding.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken).ConfigureAwait(false);
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        IChannel[] channels;
        lock (consumerChannelsGate)
        {
            channels = consumerChannels.ToArray();
            consumerChannels.Clear();
        }

        foreach (var channel in channels)
        {
            await channel.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        RabbitMqBinding binding,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = eventArgs.Body.ToArray();
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();
            var result = await processor.ProcessAsync(body, name, binding.HandlerIdentity, cancellationToken: cancellationToken).ConfigureAwait(false);
            var settled = ApplyRetryBudget(result, busOptions.Get(name).MaxDeliveryAttempts);
            await NotifyAsync(settled, binding.HandlerIdentity, body, cancellationToken).ConfigureAwait(false);

            switch (settled.Action)
            {
                case MessageProcessingAction.Complete:
                    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken).ConfigureAwait(false);
                    return;

                case MessageProcessingAction.Retry:
                {
                    var retryBody = BuildRetryBody(body);
                    await PublishAsync(
                        channel,
                        binding.RetryExchangeName,
                        binding.RetryRoutingKey,
                        retryBody,
                        settled.Context,
                        cancellationToken).ConfigureAwait(false);
                    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken).ConfigureAwait(false);
                    return;
                }

                case MessageProcessingAction.DeadLetter:
                    await PublishAsync(
                        channel,
                        binding.DeadLetterExchangeName,
                        binding.DeadLetterRoutingKey,
                        body,
                        settled.Context,
                        cancellationToken).ConfigureAwait(false);
                    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken).ConfigureAwait(false);
                    return;
            }
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PublishAsync(
        IChannel channel,
        string exchangeName,
        string routingKey,
        ReadOnlyMemory<byte> body,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = context.MessageId,
            CorrelationId = context.CorrelationId,
            ContentType = "application/json",
            Type = context.MessageName,
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private ReadOnlyMemory<byte> BuildRetryBody(ReadOnlyMemory<byte> body)
    {
        var envelope = serializer.Deserialize(body, name, registry);
        return serializer.Serialize(
            envelope.Descriptor,
            envelope.Message,
            envelope.Context.WithAttempt(envelope.Context.Attempt + 1));
    }

    private async Task NotifyAsync(MessageProcessingResult result, string handlerIdentity, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            await observer.OnSettledAsync(
                result.Context,
                result.Action,
                name,
                handlerIdentity,
                result.Errors,
                result.Reason,
                body,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static MessageProcessingResult ApplyRetryBudget(MessageProcessingResult result, int maxDeliveryAttempts)
    {
        if (result.Action != MessageProcessingAction.Retry)
        {
            return result;
        }

        return result.Context.Attempt + 1 >= maxDeliveryAttempts
            ? MessageProcessingResult.DeadLetter(
                result.Context,
                result.Errors,
                BuildRetryBudgetReason(result.Reason, maxDeliveryAttempts))
            : result;
    }

    private static string BuildRetryBudgetReason(string? reason, int maxDeliveryAttempts)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? $"Retry budget exhausted after {maxDeliveryAttempts} delivery attempts."
            : $"{reason} Retry budget exhausted after {maxDeliveryAttempts} delivery attempts.";
    }

    private IEnumerable<RabbitMqBinding> GetBindings()
    {
        var applicationName = messagingOptions.Value.ApplicationName
            ?? throw new InvalidOperationException($"RabbitMQ bus '{name}' requires MessagingOptions.ApplicationName when consumers are enabled.");

        return topologyManifest.Messages
            .Where(topology => topology.Subscriptions.Count > 0)
            .Where(topology => string.Equals(routeResolver.Resolve(topology.Message), name, StringComparison.Ordinal))
            .SelectMany(topology => topology.Subscriptions.Select(subscription =>
            {
                var queueName = subscription.GetSubscriptionName(applicationName);
                var entityName = conventions.GetEntityName(topology.Message);
                var retryEntityName = MessageTopologyNames.Retry(queueName, "default");
                var deadLetterEntityName = MessageTopologyNames.DeadLetter(queueName);

                return new RabbitMqBinding(
                    queueName,
                    subscription.Identity,
                    entityName,
                    topology.Message.Kind == MessageKind.Command ? entityName : string.Empty,
                    retryEntityName,
                    retryEntityName,
                    deadLetterEntityName,
                    deadLetterEntityName);
            }))
            .ToArray();
    }

    private sealed record RabbitMqBinding(
        string QueueName,
        string HandlerIdentity,
        string ExchangeName,
        string QueueRoutingKey,
        string RetryExchangeName,
        string RetryRoutingKey,
        string DeadLetterExchangeName,
        string DeadLetterRoutingKey);
}
