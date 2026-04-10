using LayerZero.Client;
using LayerZero.Core;
using LayerZero.MinimalApi.Contracts.Todos;

namespace LayerZero.MinimalApi.Client.Sample.Clients;

public sealed class TodosClient(HttpClient httpClient)
{
    private readonly LayerZeroClient client = new(httpClient, TodosJsonContext.Default);

    public ValueTask<Result<IReadOnlyList<Todo>>> ListAsync(
        bool? includeCompleted = null,
        CancellationToken cancellationToken = default)
    {
        return client.SendAsync(ListTodos.Endpoint, new ListTodos.Request(includeCompleted), cancellationToken);
    }

    public ValueTask<ApiResponse<IReadOnlyList<Todo>>> ListForResponseAsync(
        bool? includeCompleted = null,
        CancellationToken cancellationToken = default)
    {
        return client.SendForResponseAsync(ListTodos.Endpoint, new ListTodos.Request(includeCompleted), cancellationToken);
    }

    public ValueTask<Result<Todo>> CreateAsync(
        CreateTodo.Request request,
        CancellationToken cancellationToken = default)
    {
        return client.SendAsync(CreateTodo.Endpoint, request, cancellationToken);
    }

    public ValueTask<ApiResponse<Todo>> CreateForResponseAsync(
        CreateTodo.Request request,
        CancellationToken cancellationToken = default)
    {
        return client.SendForResponseAsync(CreateTodo.Endpoint, request, cancellationToken);
    }

    public ValueTask<Result<Todo>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return client.SendAsync(GetTodo.Endpoint, new GetTodo.Request(id), cancellationToken);
    }

    public ValueTask<ApiResponse<Todo>> GetForResponseAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return client.SendForResponseAsync(GetTodo.Endpoint, new GetTodo.Request(id), cancellationToken);
    }

    public ValueTask<Result<Todo>> CompleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return client.SendAsync(CompleteTodo.Endpoint, new CompleteTodo.Request(id), cancellationToken);
    }

    public ValueTask<ApiResponse<Todo>> CompleteForResponseAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return client.SendForResponseAsync(CompleteTodo.Endpoint, new CompleteTodo.Request(id), cancellationToken);
    }
}
