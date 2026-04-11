using LayerZero.Core;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace LayerZero.Fulfillment.Api.Features.Orders.Cancel;

public static class CancelOrderEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                OrderRoutes.Cancel,
                async (
                    Guid id,
                    [FromBody] CancelOrderApi.Body body,
                    [FromServices] Handler handler,
                    HttpContext httpContext) =>
                {
                    var result = await handler.HandleAsync(new CancelOrderApi.Request(id, body.Reason), httpContext.RequestAborted).ConfigureAwait(false);
                    return result.IsSuccess
                        ? Results.Accepted()
                        : Results.Problem(title: "Order cancellation failed.", detail: string.Join("; ", result.Errors.Select(static error => error.Message)));
                })
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    public sealed class Handler(ICommandSender sender) : IAsyncRequestHandler<CancelOrderApi.Request, Unit>
    {
        public async ValueTask<Result<Unit>> HandleAsync(CancelOrderApi.Request request, CancellationToken cancellationToken = default)
        {
            var result = await sender.SendAsync(new CancelOrder(request.OrderId, request.Reason), cancellationToken).ConfigureAwait(false);
            return result.IsFailure
                ? Result<Unit>.Failure(result.Errors)
                : Result<Unit>.Success(Unit.Value);
        }
    }
}
