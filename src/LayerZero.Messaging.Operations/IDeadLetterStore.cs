namespace LayerZero.Messaging.Operations;

/// <summary>
/// Stores and retrieves archived dead-letter messages.
/// </summary>
public interface IDeadLetterStore
{
    /// <summary>
    /// Lists archived dead-letter records.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The archived dead-letter records.</returns>
    Task<IReadOnlyList<DeadLetterEntry>> GetDeadLettersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one archived dead-letter envelope.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="handlerIdentity">The optional handler identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The archived envelope when found; otherwise <see langword="null"/>.</returns>
    Task<DeadLetterEnvelope?> GetEnvelopeAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks one archived dead-letter record as requeued.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="handlerIdentity">The optional handler identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    Task MarkRequeuedAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default);
}
