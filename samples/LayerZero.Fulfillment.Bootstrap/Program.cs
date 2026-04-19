using LayerZero.Fulfillment.Bootstrap;
using LayerZero.Fulfillment.Processing;
using LayerZero.Fulfillment.Projections;
using LayerZero.Fulfillment.Shared;
using LayerZero.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
FulfillmentBootstrapHost.ConfigureServices(builder.Services, builder.Configuration);

if (await builder.RunLayerZeroMigrationsCommandAsync(args, builder.Build) is { } exitCode)
{
    return exitCode;
}

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LayerZero.Fulfillment.Bootstrap");
var broker = builder.Configuration["Messaging:Broker"] ?? "RabbitMq";

logger.LogInformation("Fulfillment migrations started for broker {Broker}.", broker);
await FulfillmentBootstrapHost.ApplyMigrationsAsync(host.Services).ConfigureAwait(false);
logger.LogInformation("Fulfillment migrations completed for broker {Broker}.", broker);

await FulfillmentProvisioning.ProvisionTopologyAsync(
    static (services, configuration) => ProcessingHost.ConfigureServices(services, configuration),
    builder.Configuration,
    operationName: "processing topology provisioning").ConfigureAwait(false);

await FulfillmentProvisioning.ProvisionTopologyAsync(
    static (services, configuration) => ProjectionHost.ConfigureServices(services, configuration),
    builder.Configuration,
    operationName: "projection topology provisioning").ConfigureAwait(false);

return 0;
