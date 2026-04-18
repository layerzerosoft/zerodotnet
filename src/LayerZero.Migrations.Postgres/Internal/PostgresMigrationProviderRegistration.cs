using LayerZero.Data.Postgres;
using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using LayerZero.Migrations.Postgres.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

[assembly: MigrationProviderRegistrar(typeof(LayerZero.Migrations.Postgres.Internal.PostgresMigrationProviderRegistration))]

namespace LayerZero.Migrations.Postgres.Internal;

internal sealed class PostgresMigrationDatabaseAdapterFactory : IMigrationDatabaseAdapterFactory
{
    public string ProviderName => PostgresDataProvider.ProviderName;

    public IMigrationDatabaseAdapter Create(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<PostgresMigrationDatabaseAdapter>(serviceProvider);
    }
}

internal sealed class PostgresMigrationProviderRegistration : IMigrationProviderRegistrar
{
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
