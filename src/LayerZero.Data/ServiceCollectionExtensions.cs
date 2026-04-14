using LayerZero.Data.Configuration;
using LayerZero.Data.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LayerZero.Data;

/// <summary>
/// Registers the shared LayerZero data foundation.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds LayerZero data services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The optional data configuration.</param>
    /// <returns>The data builder.</returns>
    public static LayerZeroDataBuilder AddLayerZeroData(
        this IServiceCollection services,
        Action<LayerZeroDataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<LayerZeroDataOptions>()
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionStringName),
                "The LayerZero data connection string name must not be empty.")
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<LayerZeroDataOptions>, LayerZeroDataOptionsSetup>());

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton<LayerZeroDataBuilderAccessor>();
        return new LayerZeroDataBuilder(services);
    }
}

internal sealed class LayerZeroDataBuilderAccessor;
