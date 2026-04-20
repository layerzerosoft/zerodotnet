using LayerZero.Fulfillment.Kafka.Projections;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
KafkaFulfillmentProjectionsHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

await host.RunAsync();
