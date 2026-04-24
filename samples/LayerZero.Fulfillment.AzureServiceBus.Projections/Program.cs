using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using LayerZero.Messaging.AzureServiceBus;
using LayerZero.Messaging.Operations;
using LayerZero.Messaging.Operations.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Environment.ApplicationName = "fulfillment-azure-service-bus";

builder.Services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
builder.Services.AddData().UsePostgres("Fulfillment");
builder.Services.AddMessagingOperations().UsePostgres("Fulfillment");
builder.Services.AddFulfillmentStore();
builder.Services.AddMessaging()
    .AddAzureServiceBus(builder.Configuration, role: MessageTransportRole.Consumers);

await builder.Build().RunAsync();
