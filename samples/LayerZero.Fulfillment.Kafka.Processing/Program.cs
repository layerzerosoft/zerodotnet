using LayerZero.Fulfillment.Kafka.Processing;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
KafkaFulfillmentProcessingHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
