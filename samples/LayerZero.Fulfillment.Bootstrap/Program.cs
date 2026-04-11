using LayerZero.Fulfillment.Processing;
using LayerZero.Fulfillment.Projections;
using LayerZero.Fulfillment.Shared;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
await FulfillmentProvisioning.InitializeStoreAsync(
    static (services, configuration) => services.AddFulfillmentStore(configuration),
    builder.Configuration);

await FulfillmentProvisioning.InitializeStoreAndProvisionAsync(
    static (services, configuration) => ProcessingHost.ConfigureServices(services, configuration),
    builder.Configuration);
await FulfillmentProvisioning.InitializeStoreAndProvisionAsync(
    static (services, configuration) => ProjectionHost.ConfigureServices(services, configuration),
    builder.Configuration);
