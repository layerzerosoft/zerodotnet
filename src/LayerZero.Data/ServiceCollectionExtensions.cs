using LayerZero.Data.Internal;
using LayerZero.Data.Configuration;
using LayerZero.Data.Internal.Execution;
using LayerZero.Data.Internal.Materialization;
using LayerZero.Data.Internal.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.CompilerServices;

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
    /// <returns>The configured data builder.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static DataBuilder AddData(this IServiceCollection services)
    {
        var scopeAssembly = Assembly.GetCallingAssembly();
        return AddDataCore(services, scopeAssembly);
    }

    private static DataBuilder AddDataCore(IServiceCollection services, Assembly? scopeAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DataOptions>().ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<DataOptions>, DataOptionsSetup>());
        services.TryAddSingleton<IEntityMapRegistry, EntityMapRegistry>();
        services.TryAddScoped<DataScopeManager>();
        services.TryAddSingleton<DataCommandCache>();
        services.TryAddSingleton<IDataMaterializerSource, DataMaterializerSource>();
        services.TryAddScoped<DataContext>();
        services.TryAddScoped<IDataContext>(static serviceProvider => serviceProvider.GetRequiredService<DataContext>());
        services.TryAddScoped<IDataSqlContext>(static serviceProvider => serviceProvider.GetRequiredService<DataContext>());
        services.TryAddScoped<IDataDispatcher, DataDispatcher>();

        DataAssemblyRegistrarCatalog.Apply(services, scopeAssembly);
        return new DataBuilder(services);
    }

    /// <summary>
     /// Adds LayerZero data services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The data configuration.</param>
    /// <returns>The current service collection.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddData(
        this IServiceCollection services,
        Action<DataBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var scopeAssembly = Assembly.GetCallingAssembly();
        var builder = AddDataCore(services, scopeAssembly);
        configure(builder);
        builder.ValidateProviderSelection();
        return services;
    }
}
