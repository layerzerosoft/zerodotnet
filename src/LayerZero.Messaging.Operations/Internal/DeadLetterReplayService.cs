using LayerZero.Messaging.Serialization;

namespace LayerZero.Messaging.Operations.Internal;

internal sealed class DeadLetterReplayService(
    IDeadLetterStore store,
    IMessageRegistry registry,
    IMessageTransportResolver transportResolver,
    MessageEnvelopeSerializer serializer) : IDeadLetterReplayService
{
    public async Task<bool> RequeueAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var envelope = await store.GetEnvelopeAsync(messageId, handlerIdentity, cancellationToken).ConfigureAwait(false);
        if (envelope is null)
        {
            return false;
        }

        var deserialized = serializer.Deserialize(envelope.Body, envelope.TransportName, registry);
        var transport = transportResolver.Resolve(deserialized.Descriptor);
        var transportMessage = new TransportMessage(
            deserialized.Descriptor,
            deserialized.Context.WithAttempt(0),
            envelope.Body);

        if (deserialized.Descriptor.Kind == MessageKind.Command)
        {
            await transport.SendAsync(transportMessage, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await transport.PublishAsync(transportMessage, cancellationToken).ConfigureAwait(false);
        }

        await store.MarkRequeuedAsync(messageId, handlerIdentity, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
