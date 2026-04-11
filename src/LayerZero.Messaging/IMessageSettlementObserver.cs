using LayerZero.Core;

namespace LayerZero.Messaging;

/// <summary>
/// Observes message settlement outcomes after a transport processes one message.
/// </summary>
public interface IMessageSettlementObserver
{
    /// <summary>
    /// Observes one settlement outcome.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="action">The settlement action.</param>
    /// <param name="transportName">The transport name.</param>
    /// <param name="handlerIdentity">The optional handler identity.</param>
    /// <param name="errors">Any associated errors.</param>
    /// <param name="reason">The optional reason.</param>
    /// <param name="body">The original transport body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask OnSettledAsync(
        MessageContext context,
        MessageProcessingAction action,
        string transportName,
        string? handlerIdentity,
        IReadOnlyList<Error> errors,
        string? reason,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default);
}
