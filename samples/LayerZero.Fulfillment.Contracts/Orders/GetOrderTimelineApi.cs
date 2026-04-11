using LayerZero.Http;

namespace LayerZero.Fulfillment.Contracts.Orders;

public static class GetOrderTimelineApi
{
    public static readonly GetEndpoint<Request, IReadOnlyList<OrderTimelineEntry>> Endpoint = HttpEndpoint
        .Get<Request, IReadOnlyList<OrderTimelineEntry>>(OrderRoutes.Timeline)
        .Route("id", static request => request.OrderId);

    public sealed record Request(Guid OrderId);
}
