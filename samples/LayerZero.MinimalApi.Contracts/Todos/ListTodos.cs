using LayerZero.Http;

namespace LayerZero.MinimalApi.Contracts.Todos;

public static class ListTodos
{
    public static readonly GetEndpoint<Request, IReadOnlyList<Todo>> Endpoint = HttpEndpoint
        .Get<Request, IReadOnlyList<Todo>>(TodoRoutes.Base)
        .Query("includeCompleted", static request => request.IncludeCompleted);

    public sealed record Request(bool? IncludeCompleted);
}
