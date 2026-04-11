using LayerZero.Messaging;

namespace LayerZero.Fulfillment.Shared;

public sealed class SqliteSettlementObserver(
    FulfillmentStore store,
    IMessageRegistry registry,
    IMessageConventions conventions) : IMessageSettlementObserver
{
    public async ValueTask OnSettledAsync(
        MessageContext context,
        MessageProcessingAction action,
        string transportName,
        string? handlerIdentity,
        IReadOnlyList<LayerZero.Core.Error> errors,
        string? reason,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        if (action != MessageProcessingAction.DeadLetter || string.IsNullOrWhiteSpace(handlerIdentity))
        {
            return;
        }

        var entityName = registry.TryGetDescriptor(context.MessageName, out var descriptor)
            ? conventions.GetEntityName(descriptor)
            : MessageTopologyNames.Entity(context.MessageKind, context.MessageName);

        await store.SaveDeadLetterAsync(
            context,
            handlerIdentity,
            transportName,
            entityName,
            errors,
            reason,
            body,
            cancellationToken).ConfigureAwait(false);
    }
}
