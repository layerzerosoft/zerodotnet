using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LayerZero.Messaging.RabbitMq;

internal sealed class RabbitMqTopologyManager(
    string name,
    RabbitMqConnectionProvider connectionProvider,
    IMessageTopologyManifest topologyManifest,
    IMessageRouteResolver routeResolver,
    IMessageConventions conventions,
    IOptions<MessagingOptions> messagingOptions) : IMessageTopologyManager
{
    private readonly string busName = name;
    private readonly RabbitMqConnectionProvider connectionProvider = connectionProvider;
    private readonly IMessageTopologyManifest topologyManifest = topologyManifest;
    private readonly IMessageRouteResolver routeResolver = routeResolver;
    private readonly IMessageConventions conventions = conventions;
    private readonly IOptions<MessagingOptions> messagingOptions = messagingOptions;

    public string Name => busName;

    public async ValueTask ValidateAsync(CancellationToken cancellationToken = default)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken).ConfigureAwait(false);

        foreach (var binding in GetBindings())
        {
            await channel.ExchangeDeclarePassiveAsync(binding.EntityName, cancellationToken).ConfigureAwait(false);
            await channel.QueueDeclarePassiveAsync(binding.QueueName, cancellationToken).ConfigureAwait(false);
            await channel.ExchangeDeclarePassiveAsync(binding.RetryExchangeName, cancellationToken).ConfigureAwait(false);
            await channel.QueueDeclarePassiveAsync(binding.RetryQueueName, cancellationToken).ConfigureAwait(false);
            await channel.ExchangeDeclarePassiveAsync(binding.DeadLetterExchangeName, cancellationToken).ConfigureAwait(false);
            await channel.QueueDeclarePassiveAsync(binding.DeadLetterQueueName, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask ProvisionAsync(CancellationToken cancellationToken = default)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken).ConfigureAwait(false);

        foreach (var binding in GetBindings())
        {
            await channel.ExchangeDeclareAsync(
                binding.EntityName,
                binding.ExchangeType,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.QueueDeclareAsync(
                binding.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueBindAsync(
                binding.QueueName,
                binding.EntityName,
                binding.QueueBindingRoutingKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.ExchangeDeclareAsync(
                binding.RetryExchangeName,
                ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueDeclareAsync(
                binding.RetryQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = (int)connectionProvider.Options.RetryDelay.TotalMilliseconds,
                    ["x-dead-letter-exchange"] = binding.EntityName,
                    ["x-dead-letter-routing-key"] = binding.QueueBindingRoutingKey,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueBindAsync(
                binding.RetryQueueName,
                binding.RetryExchangeName,
                binding.RetryRoutingKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.ExchangeDeclareAsync(
                binding.DeadLetterExchangeName,
                ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueDeclareAsync(
                binding.DeadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueBindAsync(
                binding.DeadLetterQueueName,
                binding.DeadLetterExchangeName,
                binding.DeadLetterRoutingKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private IEnumerable<RabbitMqBinding> GetBindings()
    {
        var applicationName = GetRequiredApplicationName();

        return topologyManifest.Messages
            .Where(topology => topology.Subscriptions.Count > 0)
            .Where(topology => string.Equals(routeResolver.Resolve(topology.Message), busName, StringComparison.Ordinal))
            .SelectMany(topology => topology.Subscriptions.Select(subscription =>
            {
                var queueName = subscription.GetSubscriptionName(applicationName);
                var entityName = conventions.GetEntityName(topology.Message);
                var retryQueueName = MessageTopologyNames.Retry(queueName, "default");
                var deadLetterQueueName = MessageTopologyNames.DeadLetter(queueName);

                return topology.Message.Kind == MessageKind.Command
                    ? new RabbitMqBinding(
                        entityName,
                        ExchangeType.Direct,
                        queueName,
                        subscription.Identity,
                        entityName,
                        retryQueueName,
                        retryQueueName,
                        retryQueueName,
                        deadLetterQueueName,
                        deadLetterQueueName,
                        deadLetterQueueName)
                    : new RabbitMqBinding(
                        entityName,
                        ExchangeType.Fanout,
                        queueName,
                        subscription.Identity,
                        string.Empty,
                        retryQueueName,
                        retryQueueName,
                        retryQueueName,
                        deadLetterQueueName,
                        deadLetterQueueName,
                        deadLetterQueueName);
            }))
            .ToArray();
    }

    private string GetRequiredApplicationName()
    {
        return messagingOptions.Value.ApplicationName
            ?? throw new InvalidOperationException($"RabbitMQ bus '{busName}' requires MessagingOptions.ApplicationName when consumers are enabled.");
    }

    private sealed record RabbitMqBinding(
        string EntityName,
        string ExchangeType,
        string QueueName,
        string HandlerIdentity,
        string QueueBindingRoutingKey,
        string RetryExchangeName,
        string RetryQueueName,
        string RetryRoutingKey,
        string DeadLetterExchangeName,
        string DeadLetterQueueName,
        string DeadLetterRoutingKey);
}
