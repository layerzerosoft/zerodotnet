using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using LayerZero.Migrations.SqlServer.Configuration;
using LayerZero.Migrations.SqlServer.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Migrations.SqlServer;

/// <summary>
/// Registers the LayerZero SQL Server migrations adapter.
/// </summary>
public static class SqlServerMigrationsBuilderExtensions
{
    /// <summary>
    /// Adds the SQL Server migrations adapter.
    /// </summary>
    /// <param name="builder">The migrations builder.</param>
    /// <param name="configure">The adapter configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public static MigrationsBuilder AddSqlServer(
        this MigrationsBuilder builder,
        Action<SqlServerMigrationsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<SqlServerMigrationsOptions>()
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "The SQL Server connection string must not be empty.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.DefaultSchema),
                "The SQL Server default schema must not be empty.")
            .Validate(static options => options.CommandTimeoutSeconds is null or > 0,
                "The SQL Server command timeout must be positive when set.")
            .Validate(static options => options.LockTimeout >= TimeSpan.Zero,
                "The SQL Server lock timeout must not be negative.")
            .ValidateOnStart();

        builder.Services.PostConfigure(configure);
        builder.Services.TryAddSingleton<IMigrationDatabaseAdapter, SqlServerMigrationDatabaseAdapter>();
        return builder;
    }
}
