using LayerZero.Bootstrap;
using LayerZero.Bootstrap.Messaging;
using LayerZero.Bootstrap.Migrations;
using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Messaging;
using LayerZero.Messaging.Operations;
using LayerZero.Messaging.Operations.Postgres;
using LayerZero.Messaging.RabbitMq;
using LayerZero.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Environment.ApplicationName = "fulfillment-rabbitmq";

builder.Services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
builder.Services.AddData()
    .UsePostgres("Fulfillment")
    .UseMigrations(options => options.Executor = "fulfillment-rabbitmq-bootstrap");
builder.Services.AddMessagingOperations().UsePostgres("Fulfillment");
builder.Services.AddMessaging()
    .AddRabbitMq(builder.Configuration, role: MessageTransportRole.Administration);
builder.AddLayerZeroBootstrap(bootstrap => bootstrap
    .AddMigrationsStep()
    .AddMessagingProvisioningStep());

if (await builder.RunLayerZeroBootstrapCommandsAsync(args).ConfigureAwait(false) is { } exitCode)
{
    return exitCode;
}

return await builder.RunLayerZeroBootstrapAsync().ConfigureAwait(false);
