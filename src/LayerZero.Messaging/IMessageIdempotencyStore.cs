namespace LayerZero.Messaging;

/// <summary>
/// Provides idempotency checkpoints for message handlers.
/// </summary>
public interface IMessageIdempotencyStore
{
    /// <summary>
    /// Attempts to begin processing for a deduplication key.
    /// </summary>
    /// <param name="deduplicationKey">The dedupe key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when processing should proceed.</returns>
    ValueTask<bool> TryBeginAsync(string deduplicationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a deduplication key as completed successfully.
    /// </summary>
    /// <param name="deduplicationKey">The dedupe key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask CompleteAsync(string deduplicationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons a deduplication key so it can be retried later.
    /// </summary>
    /// <param name="deduplicationKey">The dedupe key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask AbandonAsync(string deduplicationKey, CancellationToken cancellationToken = default);
}
