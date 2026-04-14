using LayerZero.Data.SqlServer;
using LayerZero.Migrations.Internal;
using LayerZero.Migrations.SqlServer.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LayerZero.Migrations.SqlServer.Internal;

internal sealed class SqlServerMigrationDatabaseAdapterFactory : IMigrationDatabaseAdapterFactory
{
    public string ProviderName => SqlServerDataProvider.ProviderName;

    public IMigrationDatabaseAdapter Create(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<SqlServerMigrationDatabaseAdapter>(serviceProvider);
    }
}

internal static class SqlServerMigrationProviderRegistration
{
    internal static void Register()
    {
        MigrationProviderRegistry.Register(SqlServerDataProvider.ProviderName, static services =>
        {
            services.AddOptions<SqlServerMigrationsOptions>()
                .Validate(static options => options.CommandTimeoutSeconds is null or > 0,
                    "The SQL Server command timeout must be positive when set.")
                .Validate(static options => options.LockTimeout >= TimeSpan.Zero,
                    "The SQL Server lock timeout must not be negative.")
                .ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IConfigureOptions<SqlServerMigrationsOptions>, SqlServerMigrationsOptionsSetup>());

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IMigrationDatabaseAdapterFactory, SqlServerMigrationDatabaseAdapterFactory>());
        });
    }
}
