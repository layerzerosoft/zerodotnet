namespace LayerZero.Messaging;

/// <summary>
/// Validates messaging topology and configuration for one named bus.
/// </summary>
public interface IMessageBusTopologyValidator
{
    /// <summary>
    /// Gets the logical bus name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Validates configuration and topology.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask ValidateAsync(CancellationToken cancellationToken = default);
}
