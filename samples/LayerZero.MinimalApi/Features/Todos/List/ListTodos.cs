using LayerZero.Core;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.AspNetCore.Http.HttpResults;
using ListTodosRequest = LayerZero.MinimalApi.Contracts.Todos.ListTodos.Request;
using TodoContract = LayerZero.MinimalApi.Contracts.Todos.Todo;
using TodoRoutes = LayerZero.MinimalApi.Contracts.Todos.TodoRoutes;

namespace LayerZero.MinimalApi.Features.Todos.List;

public static class ListTodos
{
    public const string EndpointName = "Todos_List";

    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup(TodoRoutes.Base).WithTags("Todos");

        group.MapGet("", async Task<Ok<IReadOnlyList<TodoContract>>> (
                bool? includeCompleted,
                IAsyncRequestHandler<ListTodosRequest, IReadOnlyList<TodoContract>> handler,
                CancellationToken cancellationToken) =>
            {
                Result<IReadOnlyList<TodoContract>> result = await handler
                    .HandleAsync(new ListTodosRequest(includeCompleted), cancellationToken)
                    .ConfigureAwait(false);

                return TypedResults.Ok(result.Value);
            })
            .WithName(EndpointName)
            .WithSummary("List todos")
            .WithDescription("List active todos by default. Omit includeCompleted for active todos only, or set includeCompleted=true to include completed todos.");
    }

    public sealed class Handler : IAsyncRequestHandler<ListTodosRequest, IReadOnlyList<TodoContract>>
    {
        private readonly ITodoRepository todos;

        public Handler(ITodoRepository todos)
        {
            this.todos = todos;
        }

        public async ValueTask<Result<IReadOnlyList<TodoContract>>> HandleAsync(
            ListTodosRequest request,
            CancellationToken cancellationToken = default)
        {
            bool includeCompleted = request.IncludeCompleted ?? false;

            IReadOnlyList<TodoItem> items = await todos
                .ListAsync(includeCompleted, cancellationToken)
                .ConfigureAwait(false);

            TodoContract[] response = items
                .Select(todo => new TodoContract(todo.Id, todo.Title, todo.DueOn, todo.IsCompleted))
                .ToArray();

            return Result<IReadOnlyList<TodoContract>>.Success(response);
        }
    }
}
