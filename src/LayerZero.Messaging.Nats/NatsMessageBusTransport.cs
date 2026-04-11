using NATS.Client.Core;
using NATS.Client.JetStream;

namespace LayerZero.Messaging.Nats;

internal sealed class NatsMessageBusTransport(
    string name,
    NatsClientProvider clientProvider,
    IMessageConventions conventions) : IMessageBusTransport
{
    public string Name { get; } = name;

    public ValueTask SendAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return PublishCoreAsync(message, cancellationToken);
    }

    public ValueTask PublishAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return PublishCoreAsync(message, cancellationToken);
    }

    private async ValueTask PublishCoreAsync(TransportMessage message, CancellationToken cancellationToken)
    {
        var headers = new NatsHeaders
        {
            ["layerzero-message-name"] = message.Context.MessageName,
            ["layerzero-message-kind"] = message.Context.MessageKind == MessageKind.Command ? "command" : "event",
        };

        if (message.Context.TraceParent is not null)
        {
            headers["traceparent"] = message.Context.TraceParent;
        }

        if (message.Context.TraceState is not null)
        {
            headers["tracestate"] = message.Context.TraceState;
        }

        if (message.Context.AffinityKey is not null)
        {
            headers["layerzero-affinity-key"] = message.Context.AffinityKey;
        }

        var publishOpts = new NatsJSPubOpts
        {
            MsgId = message.Context.MessageId,
        };

        await (await clientProvider.GetJetStreamAsync(cancellationToken).ConfigureAwait(false))
            .PublishAsync(
                conventions.GetEntityName(message.Descriptor),
                message.Body.ToArray(),
                headers: headers,
                opts: publishOpts,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
