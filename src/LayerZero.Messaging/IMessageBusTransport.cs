namespace LayerZero.Messaging;

/// <summary>
/// Sends LayerZero transport messages through one named bus.
/// </summary>
public interface IMessageBusTransport
{
    /// <summary>
    /// Gets the logical bus name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Sends a command transport message.
    /// </summary>
    /// <param name="message">The transport message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask SendAsync(TransportMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event transport message.
    /// </summary>
    /// <param name="message">The transport message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask PublishAsync(TransportMessage message, CancellationToken cancellationToken = default);
}
