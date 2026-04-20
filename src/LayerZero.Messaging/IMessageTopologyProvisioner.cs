namespace LayerZero.Messaging;

/// <summary>
/// Coordinates topology validation and provisioning across all registered buses.
/// </summary>
public interface IMessageTopologyProvisioner
{
    /// <summary>
    /// Validates all registered messaging topologies.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask ValidateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions all registered messaging topologies.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask ProvisionAsync(CancellationToken cancellationToken = default);
}
