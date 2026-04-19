using System.Reflection;
using LayerZero.AspNetCore;
using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using Microsoft.OpenApi;

const string DefaultFulfillmentConnectionString = "Host=localhost;Port=5432;Database=fulfillment;Username=postgres;Password=postgres";

var builder = WebApplication.CreateBuilder(args);
var isOpenApiDocumentGeneration =
    string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "dotnet-getdocument", StringComparison.OrdinalIgnoreCase)
    || AppDomain.CurrentDomain.GetAssemblies().Any(static assembly =>
        assembly.GetName().Name?.Contains("GetDocument", StringComparison.OrdinalIgnoreCase) == true);
var disableTransportSetting = builder.Configuration["Messaging:DisableTransport"]
    ?? builder.WebHost.GetSetting("Messaging:DisableTransport");
var disableTransport = isOpenApiDocumentGeneration
    || (bool.TryParse(disableTransportSetting, out var disableTransportValue) && disableTransportValue);
var fulfillmentConnectionString = FulfillmentConnectionStringResolver.Resolve(
    builder.Configuration,
    fallbackConnectionString: DefaultFulfillmentConnectionString,
    overrideConnectionString: builder.WebHost.GetSetting("ConnectionStrings:Fulfillment"));

builder.Services.AddOpenApi("v1", options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
});

builder.Services.AddData(data =>
{
    data.UsePostgres(options =>
    {
        options.ConnectionString = fulfillmentConnectionString;
        options.ConnectionStringName = "Fulfillment";
        options.DefaultSchema = "public";
    });
});
builder.Services.AddFulfillmentStore();
builder.Services.AddSingleton<IMessageTopologyManifest, FulfillmentTopologyManifest>();

if (disableTransport)
{
    builder.Services.AddMessaging(options => options.ApplicationName = "fulfillment-api-openapi");
    builder.Services.AddSingleton(new MessageBusRegistration("openapi", typeof(OpenApiMessageBusTransport)));
    builder.Services.AddKeyedSingleton<IMessageBusTransport>("openapi", static (_, _) => new OpenApiMessageBusTransport());
}
else
{
    builder.Services.AddFulfillmentMessaging(
        builder.Configuration,
        FulfillmentMessagingRegistration.ResolveApplicationName(builder.Configuration, "api", "fulfillment-api"));
}

builder.Services
    .AddLayerZero()
    .AddSlices();

var app = builder.Build();

app.MapOpenApi("/openapi/{documentName}.json");
app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();
app.MapSlices();

app.Run();

public partial class Program;

internal sealed class OpenApiMessageBusTransport : IMessageBusTransport
{
    public string Name => "openapi";

    public ValueTask SendAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask PublishAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
