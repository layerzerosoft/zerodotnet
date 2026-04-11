using LayerZero.Http;

namespace LayerZero.Fulfillment.Contracts.Orders;

public static class ListDeadLettersApi
{
    public static readonly GetEndpoint<Request, IReadOnlyList<DeadLetterRecord>> Endpoint = HttpEndpoint
        .Get<Request, IReadOnlyList<DeadLetterRecord>>(OrderRoutes.DeadLetters);

    public sealed record Request();
}

public static class RequeueDeadLetterApi
{
    public static readonly PostEndpoint<Request> Endpoint = HttpEndpoint
        .Post<Request>(OrderRoutes.RequeueDeadLetter)
        .Route("messageId", static request => request.MessageId)
        .Query("handlerIdentity", static request => request.HandlerIdentity);

    public sealed record Request(string MessageId, string? HandlerIdentity = null);
}
