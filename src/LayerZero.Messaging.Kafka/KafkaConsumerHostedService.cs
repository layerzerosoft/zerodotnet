using Confluent.Kafka;
using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.Kafka.Configuration;
using LayerZero.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Kafka;

internal sealed class KafkaConsumerHostedService(
    string name,
    KafkaClientProvider clientProvider,
    IMessageTopologyManifest topologyManifest,
    IMessageRouteResolver routeResolver,
    IMessageConventions conventions,
    IOptions<MessagingOptions> messagingOptions,
    IOptionsMonitor<KafkaBusOptions> busOptions,
    IServiceScopeFactory scopeFactory,
    IMessageRegistry registry,
    MessageEnvelopeSerializer serializer,
    IEnumerable<IMessageSettlementObserver> observers) : BackgroundService
{
    private readonly IMessageSettlementObserver[] observers = observers.ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!busOptions.Get(name).EnableConsumers)
        {
            return;
        }

        var tasks = GetBindings()
            .Select(binding => Task.Run(() => RunConsumerAsync(binding, stoppingToken), stoppingToken))
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task RunConsumerAsync(KafkaBinding binding, CancellationToken cancellationToken)
    {
        using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = clientProvider.Options.BootstrapServers,
            GroupId = binding.ConsumerGroup,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();

        consumer.Subscribe([binding.TopicName, binding.RetryTopicName]);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = consumer.Consume(clientProvider.Options.PollInterval);
                if (result is null)
                {
                    await Task.Yield();
                    continue;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();
                var processing = await processor.ProcessAsync(
                    result.Message.Value,
                    name,
                    binding.HandlerIdentity,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                var settled = ApplyRetryBudget(processing, clientProvider.Options.MaxDeliveryAttempts);
                await NotifyAsync(settled, binding.HandlerIdentity, result.Message.Value, cancellationToken).ConfigureAwait(false);

                switch (settled.Action)
                {
                    case MessageProcessingAction.Complete:
                        consumer.StoreOffset(result);
                        consumer.Commit(result);
                        break;

                    case MessageProcessingAction.Retry:
                        await PublishAsync(binding.RetryTopicName, result.Message.Key, BuildRetryBody(result.Message.Value), cancellationToken).ConfigureAwait(false);
                        consumer.StoreOffset(result);
                        consumer.Commit(result);
                        break;

                    case MessageProcessingAction.DeadLetter:
                        await PublishAsync(binding.DeadLetterTopicName, result.Message.Key, result.Message.Value, cancellationToken).ConfigureAwait(false);
                        consumer.StoreOffset(result);
                        consumer.Commit(result);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task PublishAsync(string topicName, string key, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        await clientProvider.GetProducer().ProduceAsync(topicName, new Message<string, byte[]>
        {
            Key = key,
            Value = body.ToArray(),
        }, cancellationToken).ConfigureAwait(false);
    }

    private ReadOnlyMemory<byte> BuildRetryBody(ReadOnlyMemory<byte> body)
    {
        var envelope = serializer.Deserialize(body, name, registry);
        return serializer.Serialize(
            envelope.Descriptor,
            envelope.Message,
            envelope.Context.WithAttempt(envelope.Context.Attempt + 1));
    }

    private IEnumerable<KafkaBinding> GetBindings()
    {
        var applicationName = messagingOptions.Value.ApplicationName
            ?? throw new InvalidOperationException($"Kafka bus '{name}' requires MessagingOptions.ApplicationName when consumers are enabled.");

        return topologyManifest.Messages
            .Where(topology => topology.Subscriptions.Count > 0)
            .Where(topology => string.Equals(routeResolver.Resolve(topology.Message), name, StringComparison.Ordinal))
            .SelectMany(topology => topology.Subscriptions.Select(subscription =>
            {
                var topicName = conventions.GetEntityName(topology.Message);
                var subscriptionName = subscription.GetSubscriptionName(applicationName);
                return new KafkaBinding(
                    topicName,
                    MessageTopologyNames.Retry(subscriptionName, "default"),
                    MessageTopologyNames.DeadLetter(subscriptionName),
                    subscriptionName,
                    subscription.Identity);
            }))
            .ToArray();
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

    private sealed record KafkaBinding(
        string TopicName,
        string RetryTopicName,
        string DeadLetterTopicName,
        string ConsumerGroup,
        string HandlerIdentity);
}
