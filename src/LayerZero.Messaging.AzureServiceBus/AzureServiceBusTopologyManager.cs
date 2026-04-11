using Azure.Messaging.ServiceBus.Administration;
using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.AzureServiceBus;

internal sealed class AzureServiceBusTopologyManager(
    string name,
    AzureServiceBusClientProvider clientProvider,
    IMessageTopologyManifest topologyManifest,
    IMessageRouteResolver routeResolver,
    IMessageConventions conventions,
    IOptions<MessagingOptions> messagingOptions) : IMessageTopologyManager
{
    private readonly string busName = name;
    private readonly AzureServiceBusClientProvider clientProvider = clientProvider;
    private readonly IMessageTopologyManifest topologyManifest = topologyManifest;
    private readonly IMessageRouteResolver routeResolver = routeResolver;
    private readonly IMessageConventions conventions = conventions;
    private readonly IOptions<MessagingOptions> messagingOptions = messagingOptions;

    public string Name => busName;

    public async ValueTask ValidateAsync(CancellationToken cancellationToken = default)
    {
        var admin = clientProvider.GetAdministrationClient();

        foreach (var topology in GetTopologies())
        {
            var entityName = AzureServiceBusNameFormatter.FormatEntityName(conventions.GetEntityName(topology.Message));
            if (topology.Message.Kind == MessageKind.Command)
            {
                if (!await admin.QueueExistsAsync(entityName, cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException($"Azure Service Bus queue '{entityName}' does not exist for bus '{busName}'.");
                }

                var queue = await admin.GetQueueAsync(entityName, cancellationToken).ConfigureAwait(false);
                var requiresSession = conventions.UsesAffinity(topology.Message);
                if (queue.Value.RequiresSession != requiresSession)
                {
                    throw new InvalidOperationException(
                        $"Azure Service Bus queue '{entityName}' requires RequiresSession={requiresSession} for bus '{busName}'.");
                }

                continue;
            }

            if (!await admin.TopicExistsAsync(entityName, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Azure Service Bus topic '{entityName}' does not exist for bus '{busName}'.");
            }

            foreach (var subscription in topology.Subscriptions)
            {
                var subscriptionName = AzureServiceBusNameFormatter.FormatSubscriptionName(GetRequiredApplicationName(), subscription.Identity);
                if (!await admin.SubscriptionExistsAsync(entityName, subscriptionName, cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException($"Azure Service Bus subscription '{subscriptionName}' does not exist for topic '{entityName}'.");
                }

                var properties = await admin.GetSubscriptionAsync(entityName, subscriptionName, cancellationToken).ConfigureAwait(false);
                var requiresSession = conventions.UsesAffinity(topology.Message);
                if (properties.Value.RequiresSession != requiresSession)
                {
                    throw new InvalidOperationException(
                        $"Azure Service Bus subscription '{subscriptionName}' requires RequiresSession={requiresSession} for topic '{entityName}'.");
                }
            }
        }
    }

    public async ValueTask ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var admin = clientProvider.GetAdministrationClient();

        foreach (var topology in GetTopologies())
        {
            var entityName = AzureServiceBusNameFormatter.FormatEntityName(conventions.GetEntityName(topology.Message));
            if (topology.Message.Kind == MessageKind.Command)
            {
                if (await admin.QueueExistsAsync(entityName, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                await admin.CreateQueueAsync(new CreateQueueOptions(entityName)
                {
                    RequiresSession = conventions.UsesAffinity(topology.Message),
                    MaxDeliveryCount = clientProvider.Options.MaxDeliveryCount,
                    DeadLetteringOnMessageExpiration = true,
                }, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!await admin.TopicExistsAsync(entityName, cancellationToken).ConfigureAwait(false))
            {
                await admin.CreateTopicAsync(new CreateTopicOptions(entityName), cancellationToken).ConfigureAwait(false);
            }

            foreach (var subscription in topology.Subscriptions)
            {
                var subscriptionName = AzureServiceBusNameFormatter.FormatSubscriptionName(GetRequiredApplicationName(), subscription.Identity);
                if (await admin.SubscriptionExistsAsync(entityName, subscriptionName, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                var options = new CreateSubscriptionOptions(entityName, subscriptionName)
                {
                    RequiresSession = conventions.UsesAffinity(topology.Message),
                    MaxDeliveryCount = clientProvider.Options.MaxDeliveryCount,
                    DeadLetteringOnMessageExpiration = true,
                };

                await admin.CreateSubscriptionAsync(options, cancellationToken).ConfigureAwait(false);
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
            ?? throw new InvalidOperationException($"Azure Service Bus bus '{busName}' requires MessagingOptions.ApplicationName when consumers are enabled.");
    }
}
