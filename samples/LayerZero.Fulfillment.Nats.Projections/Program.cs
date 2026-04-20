using LayerZero.Fulfillment.Nats.Projections;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
NatsFulfillmentProjectionsHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
