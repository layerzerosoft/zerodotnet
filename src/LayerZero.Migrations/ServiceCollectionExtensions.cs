using LayerZero.Data.Configuration;
using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LayerZero.Migrations;

/// <summary>
/// Registers LayerZero relational migration services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enables LayerZero migrations for the active data provider.
    /// </summary>
    /// <param name="builder">The data builder.</param>
    /// <param name="configure">The optional migrations configuration.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static DataBuilder UseMigrations(
        this DataBuilder builder,
        Action<MigrationsOptions>? configure = null)
    {
        var scopeAssembly = Assembly.GetCallingAssembly();
        return UseMigrations(builder, scopeAssembly, configure);
    }

    /// <summary>
    /// Enables LayerZero migrations for the active data provider using an explicit discovery scope assembly.
    /// </summary>
    /// <param name="builder">The data builder.</param>
    /// <param name="scopeAssembly">The assembly whose generated LayerZero migrations should anchor discovery.</param>
    /// <param name="configure">The optional migrations configuration.</param>
    public static DataBuilder UseMigrations(
        this DataBuilder builder,
        Assembly scopeAssembly,
        Action<MigrationsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(scopeAssembly);

        builder.Services.AddOptions<MigrationsOptions>()
            .Validate(static options => !string.IsNullOrWhiteSpace(options.HistoryTableSchema),
                "The migration history schema must not be empty.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.HistoryTableName),
                "The migration history table name must not be empty.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.LockName),
                "The migration lock name must not be empty.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Executor),
                "The migration executor name must not be empty.")
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<MigrationsOptions>, MigrationsOptionsSetup>());

        if (configure is not null)
        {
            builder.Services.PostConfigure(configure);
        }

        MigrationAssemblyRegistrarCatalog.Apply(builder.Services, scopeAssembly);
        var selectedProvider = builder.GetSelectedProvider();
        MigrationProviderRegistry.Apply(builder.Services, selectedProvider.Name, selectedProvider.MigrationsAssemblyName);

        builder.Services.TryAddSingleton<MigrationModelCompiler>();
        builder.Services.TryAddSingleton<IMigrationDatabaseAdapterResolver, MigrationDatabaseAdapterResolver>();
        builder.Services.TryAddSingleton<IMigrationRuntime>(static serviceProvider =>
            new MigrationRuntime(
                serviceProvider.GetRequiredService<IMigrationCatalog>(),
                serviceProvider.GetRequiredService<IMigrationDatabaseAdapterResolver>().Resolve(),
                serviceProvider.GetRequiredService<MigrationModelCompiler>(),
                serviceProvider.GetRequiredService<IOptions<MigrationsOptions>>()));

        return builder;
    }

    /// <summary>
    /// Enables LayerZero migrations for the active data provider using the assembly that contains <typeparamref name="TScopeMarker" />.
    /// </summary>
    /// <typeparam name="TScopeMarker">A marker type from the desired discovery scope assembly.</typeparam>
    /// <param name="builder">The data builder.</param>
    /// <param name="configure">The optional migrations configuration.</param>
    public static DataBuilder UseMigrations<TScopeMarker>(
        this DataBuilder builder,
        Action<MigrationsOptions>? configure = null)
    {
        return UseMigrations(builder, typeof(TScopeMarker).Assembly, configure);
    }
}
