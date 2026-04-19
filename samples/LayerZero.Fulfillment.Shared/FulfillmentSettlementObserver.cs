using LayerZero.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Fulfillment.Shared;

public sealed class FulfillmentSettlementObserver(
    IServiceScopeFactory scopeFactory,
    IMessageRegistry registry,
    IMessageConventions conventions) : IMessageSettlementObserver
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
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

        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<FulfillmentStore>();
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
