using LayerZero.Http;

namespace LayerZero.Fulfillment.Contracts.Orders;

public static class GetOrderApi
{
    public static readonly GetEndpoint<Request, OrderDetails> Endpoint = HttpEndpoint
        .Get<Request, OrderDetails>(OrderRoutes.Resource)
        .Route("id", static request => request.OrderId);

    public sealed record Request(Guid OrderId);
}
