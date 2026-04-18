using LayerZero.Data.Configuration;
using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
    public static void UseMigrations(
        this DataBuilder builder,
        Action<MigrationsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

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

        MigrationProviderRegistry.Apply(builder.Services);

        builder.Services.TryAddSingleton<MigrationModelCompiler>();
        builder.Services.TryAddSingleton<IMigrationCatalog>(static _ => MigrationCatalogLoader.LoadFromEntryAssembly());
        builder.Services.TryAddSingleton<IMigrationDatabaseAdapterResolver, MigrationDatabaseAdapterResolver>();
        builder.Services.TryAddSingleton<IMigrationRuntime>(static serviceProvider =>
            new MigrationRuntime(
                serviceProvider.GetRequiredService<IMigrationCatalog>(),
                serviceProvider.GetRequiredService<IMigrationDatabaseAdapterResolver>().Resolve(),
                serviceProvider.GetRequiredService<MigrationModelCompiler>(),
                serviceProvider.GetRequiredService<IOptions<MigrationsOptions>>()));
    }
}
