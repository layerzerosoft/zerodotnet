using LayerZero.Fulfillment.Shared;
#pragma warning disable IDE0005 // Required for the source-generated AddMessages() extension.
using LayerZero.Messaging;
#pragma warning restore IDE0005
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LayerZero.Fulfillment.Processing;

public static class ProcessingHost
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
        services.AddFulfillmentStore(configuration);
        services.AddFulfillmentMessaging(
                configuration,
                FulfillmentMessagingRegistration.ResolveApplicationName(configuration, "processing", "fulfillment-processing"))
            .Services
            .AddMessages();
    }
}
