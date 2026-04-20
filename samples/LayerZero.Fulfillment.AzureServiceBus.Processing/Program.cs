using LayerZero.Fulfillment.AzureServiceBus.Processing;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
AzureServiceBusFulfillmentProcessingHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
