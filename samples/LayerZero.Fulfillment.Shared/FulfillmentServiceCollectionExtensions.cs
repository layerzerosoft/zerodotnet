using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Fulfillment.Shared;

public static class FulfillmentServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<FulfillmentStore>();
        return services;
    }
}
