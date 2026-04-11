using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LayerZero.Messaging.AzureServiceBus;

internal sealed class AzureServiceBusHealthCheck(string name, AzureServiceBusClientProvider clientProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await clientProvider.GetAdministrationClient().GetNamespacePropertiesAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"Azure Service Bus '{name}' is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"Azure Service Bus '{name}' is unavailable.", exception);
        }
    }
}
