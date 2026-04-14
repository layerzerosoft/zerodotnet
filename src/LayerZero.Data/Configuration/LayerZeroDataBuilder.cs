using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Configuration;

/// <summary>
/// Builds LayerZero data services.
/// </summary>
public sealed class LayerZeroDataBuilder
{
    internal LayerZeroDataBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Applies additional data configuration.
    /// </summary>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public LayerZeroDataBuilder Configure(Action<LayerZeroDataOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Services.PostConfigure(configure);
        return this;
    }
}
