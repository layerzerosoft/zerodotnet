using LayerZero.Fulfillment.Kafka.Api;

var builder = WebApplication.CreateBuilder(args);
KafkaFulfillmentApiHost.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();
KafkaFulfillmentApiHost.ConfigureApplication(app);

app.Run();

public partial class Program;
