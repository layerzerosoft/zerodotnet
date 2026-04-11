using LayerZero.Http;

namespace LayerZero.Fulfillment.Contracts.Orders;

public static class CancelOrderApi
{
    public static readonly PostEndpoint<Request> Endpoint = HttpEndpoint
        .Post<Request>(OrderRoutes.Cancel)
        .Route("id", static request => request.OrderId)
        .JsonBody(static request => new Body(request.Reason));

    public sealed record Request(Guid OrderId, string Reason);

    public sealed record Body(string Reason);
}
