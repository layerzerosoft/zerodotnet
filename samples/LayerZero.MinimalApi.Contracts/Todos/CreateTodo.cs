using LayerZero.Http;

namespace LayerZero.MinimalApi.Contracts.Todos;

public static class CreateTodo
{
    public static readonly PostEndpoint<Request, Todo> Endpoint = HttpEndpoint
        .Post<Request, Todo>(TodoRoutes.Base)
        .JsonBody(static request => request);

    public sealed record Request(string? Title, DateOnly? DueOn);
}
