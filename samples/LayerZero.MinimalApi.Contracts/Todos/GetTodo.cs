using LayerZero.Http;

namespace LayerZero.MinimalApi.Contracts.Todos;

public static class GetTodo
{
    public static readonly GetEndpoint<Request, Todo> Endpoint = HttpEndpoint
        .Get<Request, Todo>(TodoRoutes.Resource)
        .Route("id", static request => request.Id);

    public sealed record Request(Guid Id);
}
