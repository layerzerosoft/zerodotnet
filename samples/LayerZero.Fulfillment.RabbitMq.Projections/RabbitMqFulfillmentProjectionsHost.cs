using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using LayerZero.Messaging.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LayerZero.Fulfillment.RabbitMq.Projections;

public static class RabbitMqFulfillmentProjectionsHost
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
        services.AddData().UsePostgres("Fulfillment");
        services.AddFulfillmentStore();
        services.AddMessaging(ResolveApplicationName(configuration))
            .AddRabbitMq(configuration, role: MessageTransportRole.Consumers);
    }

    private static string ResolveApplicationName(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var rootName = configuration["Messaging:ApplicationName"];
        return string.IsNullOrWhiteSpace(rootName)
            ? "fulfillment-rabbitmq"
            : rootName;
    }
}
