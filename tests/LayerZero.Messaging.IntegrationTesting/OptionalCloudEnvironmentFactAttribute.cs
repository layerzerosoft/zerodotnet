namespace LayerZero.Messaging.IntegrationTesting;

/// <summary>
/// Marks tests that may be skipped when optional cloud test infrastructure is not configured locally.
/// </summary>
public sealed class OptionalCloudEnvironmentFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptionalCloudEnvironmentFactAttribute"/> class.
    /// </summary>
    public OptionalCloudEnvironmentFactAttribute()
    {
        Skip = "Set LAYERZERO_AZURE_SERVICE_BUS_CLOUD_CONNECTION_STRING to run optional Azure Service Bus cloud parity tests.";
        SkipWhen = "SkipWhenCloudEnvironmentUnavailable";
    }
}
