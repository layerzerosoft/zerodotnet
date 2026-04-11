using LayerZero.Client;
using LayerZero.Core;
using LayerZero.Fulfillment.Contracts.Orders;

namespace LayerZero.Fulfillment.Client.Sample.Clients;

public sealed class FulfillmentClient(HttpClient httpClient)
{
    private readonly LayerZeroClient client = new(httpClient, FulfillmentJsonContext.Default);

    public ValueTask<Result<PlaceOrderApi.Accepted>> PlaceOrderAsync(PlaceOrderApi.Request request, CancellationToken cancellationToken = default)
        => client.SendAsync(PlaceOrderApi.Endpoint, request, cancellationToken);

    public ValueTask<ApiResponse<OrderDetails>> GetOrderForResponseAsync(Guid orderId, CancellationToken cancellationToken = default)
        => client.SendForResponseAsync(GetOrderApi.Endpoint, new GetOrderApi.Request(orderId), cancellationToken);

    public ValueTask<Result<IReadOnlyList<OrderTimelineEntry>>> GetTimelineAsync(Guid orderId, CancellationToken cancellationToken = default)
        => client.SendAsync(GetOrderTimelineApi.Endpoint, new GetOrderTimelineApi.Request(orderId), cancellationToken);
}
