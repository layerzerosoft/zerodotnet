using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LayerZero.Messaging.Nats;

internal sealed class NatsHealthCheck(string name, NatsClientProvider clientProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await (await clientProvider.GetClientAsync(cancellationToken).ConfigureAwait(false)).PingAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"NATS bus '{name}' is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"NATS bus '{name}' is unavailable.", exception);
        }
    }
}
