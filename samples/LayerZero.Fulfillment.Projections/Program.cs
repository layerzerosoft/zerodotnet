using LayerZero.Fulfillment.Projections;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
ProjectionHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
