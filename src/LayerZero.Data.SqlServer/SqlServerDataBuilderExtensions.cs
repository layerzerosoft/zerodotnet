using LayerZero.Data.Internal.Sql;
using LayerZero.Data.Configuration;
using LayerZero.Data.SqlServer.Configuration;
using LayerZero.Data.SqlServer.Internal.Execution;
using LayerZero.Data.SqlServer.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.SqlServer;

/// <summary>
/// Registers LayerZero SQL Server data services.
/// </summary>
public static class SqlServerDataBuilderExtensions
{
    /// <summary>
    /// Uses SQL Server as the active LayerZero data provider.
    /// </summary>
    /// <param name="builder">The data builder.</param>
    /// <param name="configure">The optional SQL Server configuration.</param>
    public static void UseSqlServer(
        this DataBuilder builder,
        Action<SqlServerDataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SelectProvider(SqlServerDataProvider.ProviderName, "LayerZero.Migrations.SqlServer");

        builder.Services.AddOptions<SqlServerDataOptions>()
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "The SQL Server connection string must not be empty.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.DefaultSchema),
                "The SQL Server default schema must not be empty.")
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<SqlServerDataOptions>, SqlServerDataOptionsSetup>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<SqlServerDataOptions>, SqlServerDataOptionsSetup>());
        builder.Services.TryAddSingleton<IDatabaseConnectionFactory, SqlServerDatabaseConnectionFactory>();
        builder.Services.TryAddSingleton<IDataSqlDialect, SqlServerDataSqlDialect>();

        if (configure is not null)
        {
            builder.Services.PostConfigure(configure);
        }
    }
}
