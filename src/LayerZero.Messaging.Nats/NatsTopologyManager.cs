using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.Options;
using NATS.Client.JetStream.Models;

namespace LayerZero.Messaging.Nats;

internal sealed class NatsTopologyManager(
    string name,
    NatsClientProvider clientProvider,
    IMessageTopologyManifest topologyManifest,
    IMessageRouteResolver routeResolver,
    IMessageConventions conventions,
    IOptions<MessagingOptions> messagingOptions) : IMessageTopologyManager
{
    private readonly string busName = name;
    private readonly NatsClientProvider clientProvider = clientProvider;
    private readonly IMessageTopologyManifest topologyManifest = topologyManifest;
    private readonly IMessageRouteResolver routeResolver = routeResolver;
    private readonly IMessageConventions conventions = conventions;
    private readonly IOptions<MessagingOptions> messagingOptions = messagingOptions;

    public string Name => busName;

    public async ValueTask ValidateAsync(CancellationToken cancellationToken = default)
    {
        var js = await clientProvider.GetJetStreamAsync(cancellationToken).ConfigureAwait(false);

        foreach (var topology in GetTopologies())
        {
            var subject = conventions.GetEntityName(topology.Message);
            var streamName = NatsJetStreamNames.Stream(subject);
            await js.GetStreamAsync(streamName, null, cancellationToken).ConfigureAwait(false);

            foreach (var subscription in topology.Subscriptions)
            {
                await js.GetConsumerAsync(
                    streamName,
                    NatsConsumerNameFormatter.Format(GetRequiredApplicationName(), subscription.Identity),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var js = await clientProvider.GetJetStreamAsync(cancellationToken).ConfigureAwait(false);

        foreach (var topology in GetTopologies())
        {
            var subject = conventions.GetEntityName(topology.Message);
            var deadLetterSubject = MessageTopologyNames.DeadLetter(subject);
            var streamName = NatsJetStreamNames.Stream(subject);
            var deadLetterStream = NatsJetStreamNames.Stream(deadLetterSubject);

            await js.CreateOrUpdateStreamAsync(new StreamConfig(streamName, [subject])
            {
                Storage = StreamConfigStorage.File,
                AllowDirect = true,
            }, cancellationToken).ConfigureAwait(false);

            await js.CreateOrUpdateStreamAsync(new StreamConfig(deadLetterStream, [deadLetterSubject])
            {
                Storage = StreamConfigStorage.File,
                AllowDirect = true,
            }, cancellationToken).ConfigureAwait(false);

            foreach (var subscription in topology.Subscriptions)
            {
                await js.CreateOrUpdateConsumerAsync(streamName, new ConsumerConfig(
                    NatsConsumerNameFormatter.Format(GetRequiredApplicationName(), subscription.Identity))
                {
                    FilterSubject = subject,
                    AckPolicy = ConsumerConfigAckPolicy.Explicit,
                    AckWait = clientProvider.Options.RetryDelay,
                    Backoff = [clientProvider.Options.RetryDelay],
                    MaxDeliver = clientProvider.Options.MaxDeliver,
                }, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private IEnumerable<MessageTopologyDescriptor> GetTopologies()
    {
        return topologyManifest.Messages
            .Where(topology => topology.Subscriptions.Count > 0)
            .Where(topology => string.Equals(routeResolver.Resolve(topology.Message), busName, StringComparison.Ordinal));
    }

    private string GetRequiredApplicationName()
    {
        return messagingOptions.Value.ApplicationName
            ?? throw new InvalidOperationException($"NATS bus '{busName}' requires MessagingOptions.ApplicationName when consumers are enabled.");
    }
}
