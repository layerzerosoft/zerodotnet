using LayerZero.Fulfillment.RabbitMq.Projections;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
RabbitMqFulfillmentProjectionsHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
