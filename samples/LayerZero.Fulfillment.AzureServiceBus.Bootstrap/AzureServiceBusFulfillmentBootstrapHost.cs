using LayerZero.Bootstrap;
using LayerZero.Bootstrap.Messaging;
using LayerZero.Bootstrap.Migrations;
using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Messaging;
using LayerZero.Messaging.AzureServiceBus;
using LayerZero.Messaging.Operations;
using LayerZero.Messaging.Operations.Postgres;
using LayerZero.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LayerZero.Fulfillment.AzureServiceBus.Bootstrap;

public static class AzureServiceBusFulfillmentBootstrapHost
{
    public static void Configure(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ConfigureServices(builder.Services, builder.Configuration);
        builder.AddLayerZeroBootstrap(bootstrap => bootstrap
            .AddMigrationsStep()
            .AddMessagingProvisioningStep());
    }

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
        services.AddData()
            .UsePostgres("Fulfillment")
            .UseMigrations(options => options.Executor = "fulfillment-azure-service-bus-bootstrap");
        services.AddMessagingOperations().UsePostgres("Fulfillment");
        services.AddMessaging(ResolveApplicationName(configuration))
            .AddAzureServiceBus(configuration, role: MessageTransportRole.Administration);
    }

    public static Task ApplyMigrationsAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<IMigrationRuntime>().ApplyAsync(cancellationToken: cancellationToken).AsTask();
    }

    public static Task ProvisionMessagingAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<IMessageTopologyProvisioner>().ProvisionAsync(cancellationToken).AsTask();
    }

    private static string ResolveApplicationName(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var rootName = configuration["Messaging:ApplicationName"];
        return string.IsNullOrWhiteSpace(rootName)
            ? "fulfillment-azure-service-bus"
            : rootName;
    }
}
