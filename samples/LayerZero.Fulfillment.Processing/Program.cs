using LayerZero.Fulfillment.Processing;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
ProcessingHost.ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<FulfillmentStore>();
    await store.InitializeAsync();

    if (builder.Configuration.GetValue<bool>("Fulfillment:ProvisionOnStart"))
    {
        foreach (var manager in scope.ServiceProvider.GetServices<IMessageTopologyManager>())
        {
            await manager.ProvisionAsync();
        }
    }
}

await host.RunAsync();
