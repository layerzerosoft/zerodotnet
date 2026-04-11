using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.Shared;

namespace LayerZero.Fulfillment.Api.Features.Orders.Timeline;

public static class GetOrderTimelineEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                OrderRoutes.Timeline,
                async (Guid id, FulfillmentStore store, HttpContext httpContext) =>
                {
                    var timeline = await store.GetTimelineAsync(id, httpContext.RequestAborted).ConfigureAwait(false);
                    return Results.Ok(timeline);
                })
            .Produces<IReadOnlyList<OrderTimelineEntry>>();
    }
}
