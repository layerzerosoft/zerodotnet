using LayerZero.AspNetCore;
using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Fulfillment.Api;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using LayerZero.Messaging.Kafka;
using LayerZero.Messaging.Operations;
using LayerZero.Messaging.Operations.Postgres;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Environment.ApplicationName = "fulfillment-kafka";

builder.Services.AddOpenApi("v1", options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
});
builder.Services.AddHealthChecks();
builder.Services.AddData().UsePostgres("Fulfillment");
builder.Services.AddMessagingOperations().UsePostgres("Fulfillment");
builder.Services.AddFulfillmentStore();
builder.Services.AddMessaging()
    .AddKafka(builder.Configuration, role: MessageTransportRole.SendOnly);
builder.Services.AddLayerZero().AddSlices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.MapOpenApi("/openapi/{documentName}.json");
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();
app.MapSlices();

app.Run();

public partial class Program;
