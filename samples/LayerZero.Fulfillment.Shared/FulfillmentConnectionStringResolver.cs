using Microsoft.Extensions.Configuration;

namespace LayerZero.Fulfillment.Shared;

public static class FulfillmentConnectionStringResolver
{
    public static string Resolve(
        IConfiguration configuration,
        string? fallbackConnectionString = null,
        string? overrideConnectionString = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetConnectionString("Fulfillment")
            ?? overrideConnectionString
            ?? configuration["ConnectionStrings:Fulfillment"]
            ?? fallbackConnectionString
            ?? throw new InvalidOperationException("The Fulfillment PostgreSQL connection string is not configured.");
    }
}
