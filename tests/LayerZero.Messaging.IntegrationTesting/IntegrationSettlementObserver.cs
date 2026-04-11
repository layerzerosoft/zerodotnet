using LayerZero.Core;

namespace LayerZero.Messaging.IntegrationTesting;

public sealed class IntegrationSettlementObserver(IntegrationState state) : IMessageSettlementObserver
{
    public ValueTask OnSettledAsync(
        MessageContext context,
        MessageProcessingAction action,
        string transportName,
        string? handlerIdentity,
        IReadOnlyList<Error> errors,
        string? reason,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        state.RecordSettlement(context, action, transportName, handlerIdentity, errors, reason);
        return ValueTask.CompletedTask;
    }
}
