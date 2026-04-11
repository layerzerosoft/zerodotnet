using LayerZero.Http;

namespace LayerZero.Fulfillment.Contracts.Orders;

public static class PlaceOrderApi
{
    public static readonly PostEndpoint<Request, Accepted> Endpoint = HttpEndpoint
        .Post<Request, Accepted>(OrderRoutes.Collection)
        .JsonBody(static request => request);

    public sealed record Request(
        string CustomerEmail,
        IReadOnlyList<OrderItem> Items,
        ShippingAddress ShippingAddress,
        OrderScenario Scenario);

    public sealed record Accepted(Guid OrderId);
}
