using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LayerZero.Messaging.Kafka;

internal sealed class KafkaHealthCheck(string name, KafkaClientProvider clientProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            clientProvider.GetAdminClient().GetMetadata(TimeSpan.FromSeconds(5));
            return Task.FromResult(HealthCheckResult.Healthy($"Kafka bus '{name}' is reachable."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Kafka bus '{name}' is unavailable.", exception));
        }
    }
}
