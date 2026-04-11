using LayerZero.Messaging;
using LayerZero.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Fulfillment.Shared;

public sealed class DeadLetterReplayService(
    FulfillmentStore store,
    IMessageRegistry registry,
    IServiceProvider services,
    IMessageRouteResolver routeResolver,
    MessageEnvelopeSerializer serializer)
{
    public async Task<bool> RequeueAsync(string messageId, string? handlerIdentity = null, CancellationToken cancellationToken = default)
    {
        var envelope = await store.GetDeadLetterEnvelopeAsync(messageId, handlerIdentity, cancellationToken).ConfigureAwait(false);
        if (envelope is null)
        {
            return false;
        }

        var deserialized = serializer.Deserialize(envelope.Value.Body, envelope.Value.TransportName, registry);
        var transport = services.GetRequiredKeyedService<IMessageBusTransport>(routeResolver.Resolve(deserialized.Descriptor));
        var transportMessage = new TransportMessage(
            deserialized.Descriptor,
            deserialized.Context.WithAttempt(0),
            envelope.Value.Body);

        if (deserialized.Descriptor.Kind == MessageKind.Command)
        {
            await transport.SendAsync(transportMessage, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await transport.PublishAsync(transportMessage, cancellationToken).ConfigureAwait(false);
        }

        await store.MarkDeadLetterRequeuedAsync(messageId, handlerIdentity, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
