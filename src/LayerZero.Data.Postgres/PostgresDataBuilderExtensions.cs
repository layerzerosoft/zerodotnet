using LayerZero.Data.Configuration;
using LayerZero.Data.Internal.Sql;
using LayerZero.Data.Postgres.Configuration;
using LayerZero.Data.Postgres.Internal;
using LayerZero.Data.Postgres.Internal.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LayerZero.Data.Postgres;

/// <summary>
/// Registers LayerZero PostgreSQL data services.
/// </summary>
public static class PostgresDataBuilderExtensions
{
    /// <summary>
    /// Uses PostgreSQL as the active LayerZero data provider.
    /// </summary>
    /// <param name="builder">The data builder.</param>
    /// <param name="connectionStringName">The logical connection string name.</param>
    /// <param name="configure">The optional PostgreSQL configuration.</param>
    /// <returns>The current builder.</returns>
    public static DataBuilder UsePostgres(
        this DataBuilder builder,
        string connectionStringName,
        Action<PostgresDataOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);
        return builder.UsePostgres(options =>
        {
            options.ConnectionStringName = connectionStringName;
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Uses PostgreSQL as the active LayerZero data provider.
    /// </summary>
    /// <param name="builder">The data builder.</param>
    /// <param name="configure">The optional PostgreSQL configuration.</param>
    public static DataBuilder UsePostgres(
        this DataBuilder builder,
        Action<PostgresDataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SelectProvider(PostgresDataProvider.ProviderName, "LayerZero.Migrations.Postgres");
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<DataOptions>, PostgresDataConventionsSetup>());

        builder.Services.AddOptions<PostgresDataOptions>()
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "The PostgreSQL connection string must not be empty.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.DefaultSchema),
                "The PostgreSQL default schema must not be empty.")
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<PostgresDataOptions>, PostgresDataOptionsSetup>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<PostgresDataOptions>, PostgresDataOptionsSetup>());
        builder.Services.TryAddSingleton(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PostgresDataOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException("The PostgreSQL connection string is not configured.");
            }

            return NpgsqlDataSource.Create(options.ConnectionString);
        });
        builder.Services.TryAddSingleton<IDatabaseConnectionFactory, PostgresDatabaseConnectionFactory>();
        builder.Services.TryAddSingleton<IDataSqlDialect, PostgresDataSqlDialect>();

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        return builder;
    }
}

internal sealed class PostgresDataConventionsSetup : IConfigureOptions<DataOptions>
{
    public void Configure(DataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Conventions.UseSnakeCaseIdentifiers();
    }
}
