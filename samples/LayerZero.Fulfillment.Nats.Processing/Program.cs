using LayerZero.Fulfillment.Nats.Processing;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
NatsFulfillmentProcessingHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
