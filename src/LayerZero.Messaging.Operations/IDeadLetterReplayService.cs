namespace LayerZero.Messaging.Operations;

/// <summary>
/// Replays archived dead-letter messages back to their configured transport.
/// </summary>
public interface IDeadLetterReplayService
{
    /// <summary>
    /// Attempts to requeue one archived dead-letter message.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="handlerIdentity">The optional handler identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the message was requeued; otherwise <see langword="false"/>.</returns>
    Task<bool> RequeueAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default);
}
