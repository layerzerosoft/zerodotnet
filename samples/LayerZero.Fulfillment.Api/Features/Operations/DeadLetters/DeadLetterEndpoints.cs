using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.Shared;

namespace LayerZero.Fulfillment.Api.Features.Operations.DeadLetters;

public static class DeadLetterEndpoints
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                OrderRoutes.DeadLetters,
                async (FulfillmentStore store, HttpContext httpContext) =>
                {
                    var records = await store.GetDeadLettersAsync(httpContext.RequestAborted).ConfigureAwait(false);
                    return Results.Ok(records);
                })
            .Produces<IReadOnlyList<DeadLetterRecord>>();

        endpoints.MapPost(
                OrderRoutes.RequeueDeadLetter,
                async (string messageId, string? handlerIdentity, DeadLetterReplayService replayService, HttpContext httpContext) =>
                {
                    var requeued = await replayService.RequeueAsync(messageId, handlerIdentity, httpContext.RequestAborted).ConfigureAwait(false);
                    return requeued ? Results.Accepted() : Results.NotFound();
                })
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);
    }
}
