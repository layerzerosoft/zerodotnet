using LayerZero.Fulfillment.AzureServiceBus.Api;

var builder = WebApplication.CreateBuilder(args);
AzureServiceBusFulfillmentApiHost.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();
AzureServiceBusFulfillmentApiHost.ConfigureApplication(app);

app.Run();

public partial class Program;
