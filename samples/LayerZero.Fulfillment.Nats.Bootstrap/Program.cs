using LayerZero.Bootstrap;
using LayerZero.Fulfillment.Nats.Bootstrap;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
NatsFulfillmentBootstrapHost.Configure(builder);

if (await builder.RunLayerZeroBootstrapCommandsAsync(args).ConfigureAwait(false) is { } exitCode)
{
    return exitCode;
}

return await builder.RunLayerZeroBootstrapAsync().ConfigureAwait(false);
