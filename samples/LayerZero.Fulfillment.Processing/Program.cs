using LayerZero.Fulfillment.Processing;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
ProcessingHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
