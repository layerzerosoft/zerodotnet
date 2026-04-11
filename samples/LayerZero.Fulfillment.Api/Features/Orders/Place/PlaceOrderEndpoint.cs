using LayerZero.AspNetCore;
using LayerZero.Core;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using LayerZero.Validation;
using Microsoft.AspNetCore.Mvc;

namespace LayerZero.Fulfillment.Api.Features.Orders.Place;

public static class PlaceOrderEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                OrderRoutes.Collection,
                async (
                    [FromBody] PlaceOrderApi.Request request,
                    [FromServices] Handler handler,
                    HttpContext httpContext) =>
                {
                    var result = await handler.HandleAsync(request, httpContext.RequestAborted).ConfigureAwait(false);
                    return result.IsSuccess
                        ? Results.Accepted($"/orders/{result.Value.OrderId}", result.Value)
                        : Results.Problem(title: "Order placement failed.", detail: string.Join("; ", result.Errors.Select(static error => error.Message)));
                })
            .Validate<PlaceOrderApi.Request>()
            .Produces<PlaceOrderApi.Accepted>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    public sealed class Handler(FulfillmentStore store, ICommandSender sender) : IAsyncRequestHandler<PlaceOrderApi.Request, PlaceOrderApi.Accepted>
    {
        public async ValueTask<Result<PlaceOrderApi.Accepted>> HandleAsync(PlaceOrderApi.Request request, CancellationToken cancellationToken = default)
        {
            var orderId = Guid.NewGuid();
            var command = new PlaceOrder(orderId, request.CustomerEmail, request.Items, request.ShippingAddress, request.Scenario);
            await store.CreateDraftOrderAsync(command, cancellationToken).ConfigureAwait(false);
            await store.AppendTimelineAsync(orderId, "api.accepted", "The API accepted the order and enqueued the workflow.", "api", GetType().FullName, cancellationToken).ConfigureAwait(false);

            var sendResult = await sender.SendAsync(command, cancellationToken).ConfigureAwait(false);
            return sendResult.IsFailure
                ? Result<PlaceOrderApi.Accepted>.Failure(sendResult.Errors)
                : Result<PlaceOrderApi.Accepted>.Success(new PlaceOrderApi.Accepted(orderId));
        }
    }

    public sealed class Validator : Validator<PlaceOrderApi.Request>
    {
        public Validator()
        {
            RuleFor("CustomerEmail", static request => request.CustomerEmail)
                .NotEmpty()
                .Must(static value => value?.Contains('@', StringComparison.Ordinal) == true, "layerzero.fulfillment.email", "CustomerEmail must contain '@'.");

            RuleFor("Items", static request => request.Items)
                .Must(static items => items is { Count: > 0 }, "layerzero.fulfillment.items", "At least one order item is required.");

            RuleFor("ShippingAddress.Line1", static request => request.ShippingAddress.Line1)
                .NotEmpty();
        }
    }
}
