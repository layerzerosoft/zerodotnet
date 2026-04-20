using LayerZero.Fulfillment.Nats.Api;

var builder = WebApplication.CreateBuilder(args);
NatsFulfillmentApiHost.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();
NatsFulfillmentApiHost.ConfigureApplication(app);

app.Run();

public partial class Program;
