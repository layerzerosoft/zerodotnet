using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Messaging.Operations;

namespace LayerZero.Fulfillment.Api.Features.Operations.DeadLetters;

public static class DeadLetterEndpoints
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                OrderRoutes.DeadLetters,
                async (IDeadLetterStore store, HttpContext httpContext) =>
                {
                    var records = await store.GetDeadLettersAsync(httpContext.RequestAborted).ConfigureAwait(false);
                    return Results.Ok(records.Select(ToContract).ToArray());
                })
            .Produces<IReadOnlyList<DeadLetterRecord>>();

        endpoints.MapPost(
                OrderRoutes.RequeueDeadLetter,
                async (string messageId, string? handlerIdentity, IDeadLetterReplayService replayService, HttpContext httpContext) =>
                {
                    var requeued = await replayService.RequeueAsync(messageId, handlerIdentity, httpContext.RequestAborted).ConfigureAwait(false);
                    return requeued ? Results.Accepted() : Results.NotFound();
                })
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static DeadLetterRecord ToContract(DeadLetterEntry entry)
    {
        return new DeadLetterRecord(
            entry.MessageId,
            entry.MessageName,
            entry.HandlerIdentity,
            entry.TransportName,
            entry.EntityName,
            entry.Attempt,
            entry.CorrelationId,
            entry.TraceParent,
            entry.Reason,
            entry.Errors,
            entry.FailedAtUtc,
            entry.Requeued);
    }
}
