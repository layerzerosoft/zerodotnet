using LayerZero.Data.SqlServer;
using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using LayerZero.Migrations.SqlServer.Configuration;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

[assembly: LayerZero.Migrations.MigrationProviderRegistrarAttribute(typeof(LayerZero.Migrations.SqlServer.Internal.SqlServerMigrationProviderRegistration))]

namespace LayerZero.Migrations.SqlServer.Internal;

internal sealed class SqlServerMigrationDatabaseAdapterFactory : IMigrationDatabaseAdapterFactory
{
    public string ProviderName => SqlServerDataProvider.ProviderName;

    public IMigrationDatabaseAdapter Create(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<SqlServerMigrationDatabaseAdapter>(serviceProvider);
    }
}

/// <summary>
/// Registers SQL Server-specific migration services for analyzer-generated startup paths.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SqlServerMigrationProviderRegistration : IMigrationProviderRegistrar
{
    /// <summary>
    /// Gets the data provider name handled by this registrar.
    /// </summary>
    public string ProviderName => SqlServerDataProvider.ProviderName;

    /// <summary>
    /// Adds SQL Server migration services to the container.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    public void Register(IServiceCollection services)
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
            ServiceDescriptor.Singleton<IPostConfigureOptions<MigrationsOptions>, SqlServerMigrationsRuntimeOptionsSetup>());

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMigrationDatabaseAdapterFactory, SqlServerMigrationDatabaseAdapterFactory>());
    }
}
