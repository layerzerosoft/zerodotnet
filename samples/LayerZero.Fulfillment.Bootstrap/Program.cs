using LayerZero.Fulfillment.Processing;
using LayerZero.Fulfillment.Projections;
using LayerZero.Fulfillment.Shared;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
await FulfillmentProvisioning.InitializeStoreAsync(
    static (services, configuration) => services.AddFulfillmentStore(configuration),
    builder.Configuration,
    operationName: "store initialization");

await FulfillmentProvisioning.InitializeStoreAndProvisionAsync(
    static (services, configuration) => ProcessingHost.ConfigureServices(services, configuration),
    builder.Configuration,
    operationName: "processing topology provisioning");
await FulfillmentProvisioning.InitializeStoreAndProvisionAsync(
    static (services, configuration) => ProjectionHost.ConfigureServices(services, configuration),
    builder.Configuration,
    operationName: "projection topology provisioning");
