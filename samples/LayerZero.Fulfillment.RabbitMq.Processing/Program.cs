using LayerZero.Fulfillment.RabbitMq.Processing;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
RabbitMqFulfillmentProcessingHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
