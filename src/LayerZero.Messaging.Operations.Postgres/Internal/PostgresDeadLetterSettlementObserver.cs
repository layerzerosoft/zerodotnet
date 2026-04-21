namespace LayerZero.Messaging.Operations.Postgres.Internal;

internal sealed class PostgresDeadLetterSettlementObserver(
    PostgresDeadLetterStore store,
    IMessageRegistry registry,
    IMessageConventions conventions) : IMessageSettlementObserver
{
    private readonly PostgresDeadLetterStore store = store;
    private readonly IMessageRegistry registry = registry;
    private readonly IMessageConventions conventions = conventions;

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

        await store.ArchiveAsync(
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
