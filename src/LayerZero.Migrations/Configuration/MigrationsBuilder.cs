using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Migrations.Configuration;

/// <summary>
/// Builds LayerZero migration services.
/// </summary>
public sealed class MigrationsBuilder
{
    internal MigrationsBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Configures the migrations options.
    /// </summary>
    /// <param name="configure">The options delegate.</param>
    /// <returns>The current builder.</returns>
    public MigrationsBuilder Configure(Action<MigrationsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Services.PostConfigure(configure);
        return this;
    }
}
