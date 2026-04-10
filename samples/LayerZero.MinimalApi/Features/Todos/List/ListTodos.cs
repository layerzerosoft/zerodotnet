using LayerZero.Core;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.AspNetCore.Http.HttpResults;
using ListTodosRequest = LayerZero.MinimalApi.Contracts.Todos.ListTodos.Request;
using TodoContract = LayerZero.MinimalApi.Contracts.Todos.Todo;
using TodoRoutes = LayerZero.MinimalApi.Contracts.Todos.TodoRoutes;

namespace LayerZero.MinimalApi.Features.Todos.List;

public static class ListTodos
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup(TodoRoutes.Base)
            .WithTags("Todos")
            .MapGet("", async Task<Ok<IReadOnlyList<TodoContract>>> (
                bool? includeCompleted,
                IAsyncRequestHandler<ListTodosRequest, IReadOnlyList<TodoContract>> handler,
                CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .HandleAsync(new ListTodosRequest(includeCompleted), cancellationToken)
                        .ConfigureAwait(false);

                    return TypedResults.Ok(result.Value);
                })
                .WithName(nameof(ListTodos))
                .WithSummary("List todos")
                .WithDescription("List active todos by default. Omit includeCompleted for active todos only, or set includeCompleted=true to include completed todos.");
    }

    public sealed class Handler(ITodoRepository todos) : IAsyncRequestHandler<ListTodosRequest, IReadOnlyList<TodoContract>>
    {
        public async ValueTask<Result<IReadOnlyList<TodoContract>>> HandleAsync(
            ListTodosRequest request,
            CancellationToken cancellationToken = default)
        {
            var includeCompleted = request.IncludeCompleted ?? false;

            var items = await todos
                .ListAsync(includeCompleted, cancellationToken)
                .ConfigureAwait(false);

            var response = items
                .Select(todo => new TodoContract(todo.Id, todo.Title, todo.DueOn, todo.IsCompleted))
                .ToArray();

            return Result<IReadOnlyList<TodoContract>>.Success(response);
        }
    }
}
