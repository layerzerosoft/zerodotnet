using System.Reflection;
using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Fulfillment.Shared;
using LayerZero.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Fulfillment.Bootstrap;

public static class FulfillmentBootstrapHost
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<IConfiguration>(configuration);
        services.AddData(data =>
        {
            data.UsePostgres(options =>
            {
                options.ConnectionString = FulfillmentConnectionStringResolver.Resolve(configuration);
                options.ConnectionStringName = "Fulfillment";
                options.DefaultSchema = "public";
            });
            data.UseMigrations(options =>
            {
                options.Executor = "fulfillment-bootstrap";
            });
        });

        services.Replace(ServiceDescriptor.Singleton<IMigrationCatalog>(static _ => CreateMigrationCatalog()));
    }

    public static async Task ApplyMigrationsAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var runtime = services.GetRequiredService<IMigrationRuntime>();
        await runtime.ApplyAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static async Task ApplyMigrationsAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var services = new ServiceCollection();
        ConfigureServices(services, configuration);

        await using var provider = services.BuildServiceProvider();
        await ApplyMigrationsAsync(provider, cancellationToken).ConfigureAwait(false);
    }

    public static Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Fulfillment"] = connectionString,
            })
            .Build();

        return ApplyMigrationsAsync(configuration, cancellationToken);
    }

    private static IMigrationCatalog CreateMigrationCatalog()
    {
        var assembly = typeof(FulfillmentBootstrapHost).Assembly;
        var attribute = assembly.GetCustomAttribute<MigrationCatalogAttribute>()
            ?? throw new InvalidOperationException(
                $"Assembly '{assembly.FullName}' does not declare a LayerZero migration catalog.");

        if (Activator.CreateInstance(attribute.CatalogType) is not IMigrationCatalog catalog)
        {
            throw new InvalidOperationException(
                $"Migration catalog type '{attribute.CatalogType.FullName}' could not be created.");
        }

        return catalog;
    }
}
