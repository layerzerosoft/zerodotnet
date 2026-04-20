using LayerZero.Data.Postgres;
using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using LayerZero.Migrations.Postgres.Configuration;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

[assembly: LayerZero.Migrations.MigrationProviderRegistrarAttribute(typeof(LayerZero.Migrations.Postgres.Internal.PostgresMigrationProviderRegistration))]

namespace LayerZero.Migrations.Postgres.Internal;

internal sealed class PostgresMigrationDatabaseAdapterFactory : IMigrationDatabaseAdapterFactory
{
    public string ProviderName => PostgresDataProvider.ProviderName;

    public IMigrationDatabaseAdapter Create(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<PostgresMigrationDatabaseAdapter>(serviceProvider);
    }
}

/// <summary>
/// Registers PostgreSQL-specific migration services for analyzer-generated startup paths.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PostgresMigrationProviderRegistration : IMigrationProviderRegistrar
{
    /// <summary>
    /// Gets the data provider name handled by this registrar.
    /// </summary>
    public string ProviderName => PostgresDataProvider.ProviderName;

    /// <summary>
    /// Adds PostgreSQL migration services to the container.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    public void Register(IServiceCollection services)
    {
        services.AddOptions<PostgresMigrationsOptions>()
            .Validate(static options => options.LockTimeout >= TimeSpan.Zero,
                "The PostgreSQL lock timeout must not be negative.")
            .Validate(static options => options.CommandTimeoutSeconds is null or > 0,
                "The PostgreSQL command timeout must be positive when set.")
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<PostgresMigrationsOptions>, PostgresMigrationsOptionsSetup>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<MigrationsOptions>, PostgresMigrationsRuntimeOptionsSetup>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMigrationDatabaseAdapterFactory, PostgresMigrationDatabaseAdapterFactory>());
    }
}
