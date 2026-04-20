using LayerZero.Fulfillment.AzureServiceBus.Bootstrap;
using LayerZero.Migrations;
using LayerZero.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
AzureServiceBusFulfillmentBootstrapHost.ConfigureServices(builder.Services, builder.Configuration);

if (await builder.RunLayerZeroMigrationsCommandAsync(args, builder.Build) is { } migrationExitCode)
{
    return migrationExitCode;
}

if (await builder.RunLayerZeroMessagingCommandAsync(args, builder.Build) is { } messagingExitCode)
{
    return messagingExitCode;
}

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LayerZero.Fulfillment.AzureServiceBus.Bootstrap");

logger.LogInformation("Fulfillment migrations started.");
await AzureServiceBusFulfillmentBootstrapHost.ApplyMigrationsAsync(host.Services).ConfigureAwait(false);
logger.LogInformation("Fulfillment migrations completed.");

logger.LogInformation("Fulfillment messaging topology provisioning started.");
await AzureServiceBusFulfillmentBootstrapHost.ProvisionMessagingAsync(host.Services).ConfigureAwait(false);
logger.LogInformation("Fulfillment messaging topology provisioning completed.");

return 0;
