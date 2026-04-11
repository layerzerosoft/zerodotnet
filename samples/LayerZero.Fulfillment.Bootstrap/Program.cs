using LayerZero.Fulfillment.Processing;
using LayerZero.Fulfillment.Projections;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddFulfillmentStore(builder.Configuration);

using (var scope = builder.Services.BuildServiceProvider().CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<FulfillmentStore>().InitializeAsync();
}

await ProvisionAsync(static (services, configuration) => ProcessingHost.ConfigureServices(services, configuration), builder.Configuration);
await ProvisionAsync(static (services, configuration) => ProjectionHost.ConfigureServices(services, configuration), builder.Configuration);

static async Task ProvisionAsync(Action<IServiceCollection, IConfiguration> configure, IConfiguration configuration)
{
    var services = new ServiceCollection();
    configure(services, configuration);
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    foreach (var manager in scope.ServiceProvider.GetServices<IMessageTopologyManager>())
    {
        await manager.ProvisionAsync();
    }
}
