namespace LayerZero.Messaging.Internal;

internal sealed class MessageTopologyProvisioner(
    IEnumerable<IMessageTopologyManager> managers) : IMessageTopologyProvisioner
{
    private readonly IMessageTopologyManager[] managers = managers
        .OrderBy(static manager => manager.Name, StringComparer.Ordinal)
        .ToArray();

    public async ValueTask ValidateAsync(CancellationToken cancellationToken = default)
    {
        foreach (var manager in managers)
        {
            await manager.ValidateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask ProvisionAsync(CancellationToken cancellationToken = default)
    {
        foreach (var manager in managers)
        {
            await manager.ProvisionAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
