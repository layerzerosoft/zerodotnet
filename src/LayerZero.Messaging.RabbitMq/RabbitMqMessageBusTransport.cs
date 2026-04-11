using RabbitMQ.Client;

namespace LayerZero.Messaging.RabbitMq;

internal sealed class RabbitMqMessageBusTransport(
    string name,
    RabbitMqConnectionProvider connectionProvider,
    IMessageConventions conventions) : IMessageBusTransport, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IChannel? publishChannel;

    public string Name { get; } = name;

    public ValueTask SendAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return PublishCoreAsync(message, cancellationToken);
    }

    public ValueTask PublishAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return PublishCoreAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (publishChannel is not null)
        {
            await publishChannel.DisposeAsync().ConfigureAwait(false);
        }

        gate.Dispose();
    }

    private async ValueTask PublishCoreAsync(TransportMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var channel = await GetPublishChannelAsync(cancellationToken).ConfigureAwait(false);
        var properties = CreateProperties(message.Context);
        var exchangeName = conventions.GetEntityName(message.Descriptor);
        var routingKey = ResolveRoutingKey(message, exchangeName);

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: message.Body,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IChannel> GetPublishChannelAsync(CancellationToken cancellationToken)
    {
        if (publishChannel is not null && publishChannel.IsOpen)
        {
            return publishChannel;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (publishChannel is not null && publishChannel.IsOpen)
            {
                return publishChannel;
            }

            if (publishChannel is not null)
            {
                await publishChannel.DisposeAsync().ConfigureAwait(false);
            }

            publishChannel = await connectionProvider.CreateChannelAsync(cancellationToken, publisherConfirmations: true).ConfigureAwait(false);
            return publishChannel;
        }
        finally
        {
            gate.Release();
        }
    }

    private static BasicProperties CreateProperties(MessageContext context)
    {
        return new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = context.MessageId,
            CorrelationId = context.CorrelationId,
            Timestamp = new AmqpTimestamp(context.Timestamp.ToUnixTimeSeconds()),
            ContentType = "application/json",
            Type = context.MessageName,
            Headers = new Dictionary<string, object?>
            {
                ["layerzero-message-kind"] = context.MessageKind == MessageKind.Command ? "command" : "event",
                ["layerzero-affinity-key"] = context.AffinityKey,
                ["traceparent"] = context.TraceParent,
                ["tracestate"] = context.TraceState,
            },
        };
    }

    private static string ResolveRoutingKey(TransportMessage message, string entityName)
    {
        return message.Descriptor.Kind == MessageKind.Command
            ? entityName
            : string.Empty;
    }
}
