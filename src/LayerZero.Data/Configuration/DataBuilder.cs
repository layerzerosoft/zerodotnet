using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Configuration;

/// <summary>
/// Configures LayerZero data services.
/// </summary>
public sealed class DataBuilder
{
    private readonly HashSet<string> providers = new(StringComparer.Ordinal);

    internal DataBuilder(IServiceCollection services)
    {
        Services = services;
    }

    internal IServiceCollection Services { get; }

    /// <summary>
    /// Applies additional data configuration.
    /// </summary>
    /// <param name="configure">The configuration delegate.</param>
    public void Configure(Action<DataOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Services.PostConfigure(configure);
    }

    internal void SelectProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        providers.Add(providerName);
    }

    internal void ValidateProviderSelection()
    {
        if (providers.Count == 1)
        {
            return;
        }

        throw providers.Count switch
        {
            0 => new InvalidOperationException("LayerZero data requires exactly one provider. Configure one provider inside services.AddData(data => { ... })."),
            _ => new InvalidOperationException($"LayerZero data supports exactly one provider per AddData call, but {providers.Count} providers were configured."),
        };
    }
}
