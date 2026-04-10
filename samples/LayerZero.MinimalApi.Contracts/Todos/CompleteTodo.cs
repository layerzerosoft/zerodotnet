using LayerZero.Http;

namespace LayerZero.MinimalApi.Contracts.Todos;

public static class CompleteTodo
{
    public static readonly PostEndpoint<Request, Todo> Endpoint = HttpEndpoint
        .Post<Request, Todo>(TodoRoutes.Completion)
        .Route("id", static request => request.Id);

    public sealed record Request(Guid Id);
}
