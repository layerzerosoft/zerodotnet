using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LayerZero.Messaging.RabbitMq;

internal sealed class RabbitMqHealthCheck(string name, RabbitMqConnectionProvider connectionProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            return connection.IsOpen
                ? HealthCheckResult.Healthy($"RabbitMQ bus '{name}' is reachable.")
                : HealthCheckResult.Unhealthy($"RabbitMQ bus '{name}' is not open.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"RabbitMQ bus '{name}' is unavailable.", exception);
        }
    }
}
