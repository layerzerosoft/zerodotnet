using LayerZero.Bootstrap;
using LayerZero.Fulfillment.AzureServiceBus.Bootstrap;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
AzureServiceBusFulfillmentBootstrapHost.Configure(builder);

if (await builder.RunLayerZeroBootstrapCommandsAsync(args).ConfigureAwait(false) is { } exitCode)
{
    return exitCode;
}

return await builder.RunLayerZeroBootstrapAsync().ConfigureAwait(false);
