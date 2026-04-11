using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
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
