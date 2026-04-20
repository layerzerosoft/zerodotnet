using LayerZero.Fulfillment.RabbitMq.Api;

var builder = WebApplication.CreateBuilder(args);
RabbitMqFulfillmentApiHost.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();
RabbitMqFulfillmentApiHost.ConfigureApplication(app);

app.Run();

public partial class Program;
