using LayerZero.Fulfillment.AzureServiceBus.Projections;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
AzureServiceBusFulfillmentProjectionsHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
