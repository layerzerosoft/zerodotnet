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
    /// <param name="connectionStringName">The logical connection string name.</param>
    /// <param name="configure">The optional SQL Server configuration.</param>
    /// <returns>The current builder.</returns>
    public static DataBuilder UseSqlServer(
        this DataBuilder builder,
        string connectionStringName,
        Action<SqlServerDataOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);
        return builder.UseSqlServer(options =>
        {
            options.ConnectionStringName = connectionStringName;
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Uses SQL Server as the active LayerZero data provider.
    /// </summary>
    /// <param name="builder">The data builder.</param>
    /// <param name="configure">The optional SQL Server configuration.</param>
    public static DataBuilder UseSqlServer(
        this DataBuilder builder,
        Action<SqlServerDataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SelectProvider(SqlServerDataProvider.ProviderName, "LayerZero.Migrations.SqlServer");
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<DataOptions>, SqlServerDataConventionsSetup>());

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
            builder.Services.Configure(configure);
        }

        return builder;
    }
}

internal sealed class SqlServerDataConventionsSetup : IConfigureOptions<DataOptions>
{
    public void Configure(DataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Conventions.UseExactIdentifiers();
    }
}
