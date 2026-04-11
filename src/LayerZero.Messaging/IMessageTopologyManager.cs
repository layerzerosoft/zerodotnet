namespace LayerZero.Messaging;

/// <summary>
/// Validates and provisions topology for one named messaging bus.
/// </summary>
public interface IMessageTopologyManager
{
    /// <summary>
    /// Gets the logical bus name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Validates the topology required by this bus.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask ValidateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions the topology required by this bus.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask ProvisionAsync(CancellationToken cancellationToken = default);
}
