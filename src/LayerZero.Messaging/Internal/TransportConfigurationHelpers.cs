using Microsoft.Extensions.Configuration;

namespace LayerZero.Messaging.Internal;

internal static class TransportConfigurationHelpers
{
    public static void Bind<TOptions>(IConfiguration configuration, string sectionPath, TOptions options)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        ArgumentNullException.ThrowIfNull(options);

        configuration.GetSection(sectionPath).Bind(options);
    }

    public static string ResolveConnectionString(
        IConfiguration configuration,
        string primaryConnectionStringName,
        string fallbackConnectionStringName,
        string? configuredValue)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryConnectionStringName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackConnectionStringName);

        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return configuredValue;
        }

        var primary = configuration.GetConnectionString(primaryConnectionStringName);
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        var fallback = configuration.GetConnectionString(fallbackConnectionStringName);
        return fallback ?? string.Empty;
    }
}
