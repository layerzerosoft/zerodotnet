using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.Nats.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NATS.Client.JetStream;

namespace LayerZero.Messaging.Nats;

internal sealed class NatsConsumerHostedService(
    string name,
    NatsClientProvider clientProvider,
    IMessageTopologyManifest topologyManifest,
    IMessageRouteResolver routeResolver,
    IMessageConventions conventions,
    IOptions<MessagingOptions> messagingOptions,
    IOptionsMonitor<NatsBusOptions> busOptions,
    IServiceScopeFactory scopeFactory,
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

    private async Task RunConsumerAsync(NatsBinding binding, CancellationToken cancellationToken)
    {
        var js = await clientProvider.GetJetStreamAsync(cancellationToken).ConfigureAwait(false);
        var consumer = await js.GetConsumerAsync(binding.StreamName, binding.ConsumerName, cancellationToken).ConfigureAwait(false);

        await foreach (var message in consumer.ConsumeAsync<byte[]>(cancellationToken: cancellationToken))
        {
            int? attempt = message.Metadata is null ? null : Math.Max(0, (int)message.Metadata.Value.NumDelivered - 1);
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();
            var result = await processor.ProcessAsync(
                message.Data,
                name,
                binding.HandlerIdentity,
                attempt,
                cancellationToken).ConfigureAwait(false);
            var settled = ApplyRetryBudget(result, busOptions.Get(name).MaxDeliver);
            await NotifyAsync(settled, binding.HandlerIdentity, message.Data, cancellationToken).ConfigureAwait(false);

            switch (settled.Action)
            {
                case MessageProcessingAction.Complete:
                    await message.AckAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;

                case MessageProcessingAction.Retry:
                    await message.NakAsync(busOptions.Get(name).RetryDelay, cancellationToken).ConfigureAwait(false);
                    break;

                case MessageProcessingAction.DeadLetter:
                    await js.PublishAsync(MessageTopologyNames.DeadLetter(binding.Subject), message.Data, cancellationToken: cancellationToken).ConfigureAwait(false);
                    await message.AckAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    private IEnumerable<NatsBinding> GetBindings()
    {
        var applicationName = messagingOptions.Value.ApplicationName
            ?? throw new InvalidOperationException($"NATS bus '{name}' requires MessagingOptions.ApplicationName when consumers are enabled.");

        return topologyManifest.Messages
            .Where(topology => topology.Subscriptions.Count > 0)
            .Where(topology => string.Equals(routeResolver.Resolve(topology.Message), name, StringComparison.Ordinal))
            .SelectMany(topology => topology.Subscriptions.Select(subscription =>
            {
                var subject = conventions.GetEntityName(topology.Message);
                return new NatsBinding(
                    subject,
                    NatsJetStreamNames.Stream(subject),
                    NatsConsumerNameFormatter.Format(applicationName, subscription.Identity),
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

    private static MessageProcessingResult ApplyRetryBudget(MessageProcessingResult result, int maxDeliver)
    {
        if (result.Action != MessageProcessingAction.Retry)
        {
            return result;
        }

        return result.Context.Attempt + 1 >= maxDeliver
            ? MessageProcessingResult.DeadLetter(
                result.Context,
                result.Errors,
                BuildRetryBudgetReason(result.Reason, maxDeliver))
            : result;
    }

    private static string BuildRetryBudgetReason(string? reason, int maxDeliver)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? $"Retry budget exhausted after {maxDeliver} delivery attempts."
            : $"{reason} Retry budget exhausted after {maxDeliver} delivery attempts.";
    }

    private sealed record NatsBinding(
        string Subject,
        string StreamName,
        string ConsumerName,
        string HandlerIdentity);
}
