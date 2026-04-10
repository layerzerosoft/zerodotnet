namespace LayerZero.Messaging;

/// <summary>
/// Processes incoming transport payloads through LayerZero handlers.
/// </summary>
public interface IMessageProcessor
{
    /// <summary>
    /// Processes an incoming transport payload.
    /// </summary>
    /// <param name="body">The transport body.</param>
    /// <param name="transportName">The logical transport name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The processing result.</returns>
    ValueTask<MessageProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> body,
        string transportName,
        CancellationToken cancellationToken = default);
}
