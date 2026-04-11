using LayerZero.Core;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.Shared;

namespace LayerZero.Fulfillment.Api.Features.Orders.Get;

public static class GetOrderEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                OrderRoutes.Resource,
                async (Guid id, Handler handler, HttpContext httpContext) =>
                {
                    var result = await handler.HandleAsync(new GetOrderApi.Request(id), httpContext.RequestAborted).ConfigureAwait(false);
                    return result.IsSuccess
                        ? Results.Ok(result.Value)
                        : Results.NotFound();
                })
            .Produces<OrderDetails>()
            .Produces(StatusCodes.Status404NotFound);
    }

    public sealed class Handler(FulfillmentStore store) : IAsyncRequestHandler<GetOrderApi.Request, OrderDetails>
    {
        public async ValueTask<Result<OrderDetails>> HandleAsync(GetOrderApi.Request request, CancellationToken cancellationToken = default)
        {
            var order = await store.GetOrderAsync(request.OrderId, cancellationToken).ConfigureAwait(false);
            return order is null
                ? Result<OrderDetails>.Failure(Error.Create("layerzero.fulfillment.not_found", "Order was not found."))
                : Result<OrderDetails>.Success(order);
        }
    }
}
