using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Messaging;
using LayerZero.Messaging.Nats;
using LayerZero.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LayerZero.Fulfillment.Nats.Bootstrap;

public static class NatsFulfillmentBootstrapHost
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
        services.AddData()
            .UsePostgres("Fulfillment")
            .UseMigrations(options => options.Executor = "fulfillment-nats-bootstrap");
        services.AddMessaging(ResolveApplicationName(configuration))
            .AddNats(configuration, role: MessageTransportRole.Administration);
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
            ? "fulfillment-nats"
            : rootName;
    }
}
