using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Migrations;

/// <summary>
/// Registers LayerZero relational migration services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the LayerZero migrations runtime.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional migrations configuration.</param>
    /// <returns>A migrations builder.</returns>
    public static MigrationsBuilder AddLayerZeroMigrations(
        this IServiceCollection services,
        Action<MigrationsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<MigrationsOptions>()
            .Validate(static options => !string.IsNullOrWhiteSpace(options.HistoryTableSchema),
                "The migration history schema must not be empty.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.HistoryTableName),
                "The migration history table name must not be empty.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.LockName),
                "The migration lock name must not be empty.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Executor),
                "The migration executor name must not be empty.")
            .ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton<MigrationModelCompiler>();
        services.TryAddSingleton<IMigrationRuntime, MigrationRuntime>();

        return new MigrationsBuilder(services);
    }
}
