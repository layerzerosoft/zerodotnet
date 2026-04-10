using LayerZero.Core;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.AspNetCore.Http.HttpResults;
using GetTodoRequest = LayerZero.MinimalApi.Contracts.Todos.GetTodo.Request;
using TodoContract = LayerZero.MinimalApi.Contracts.Todos.Todo;
using TodoRoutes = LayerZero.MinimalApi.Contracts.Todos.TodoRoutes;

namespace LayerZero.MinimalApi.Features.Todos.Get;

public static class GetTodo
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGroup(TodoRoutes.Base).WithTags("Todos")
            .MapGet(TodoRoutes.ById, async Task<Results<Ok<TodoContract>, NotFound>> (
                Guid id,
                IAsyncRequestHandler<GetTodoRequest, TodoContract> handler,
                CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .HandleAsync(new GetTodoRequest(id), cancellationToken)
                        .ConfigureAwait(false);

                    if (result.IsFailure)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(result.Value);
                })
                .WithName(nameof(GetTodo))
                .WithSummary("Get a todo")
                .WithDescription("Return one todo by id.");
    }

    public sealed class Handler(ITodoRepository todos) : IAsyncRequestHandler<GetTodoRequest, TodoContract>
    {
        private readonly ITodoRepository todos = todos;

        public async ValueTask<Result<TodoContract>> HandleAsync(
            GetTodoRequest request,
            CancellationToken cancellationToken = default)
        {
            var todo = await todos
                .FindAsync(request.Id, cancellationToken)
                .ConfigureAwait(false);

            if (todo is null)
            {
                return Result<TodoContract>.Failure(Error.Create(
                    "todos.not_found",
                    "Todo was not found.",
                    nameof(GetTodoRequest.Id)));
            }

            return Result<TodoContract>.Success(new TodoContract(todo.Id, todo.Title, todo.DueOn, todo.IsCompleted));
        }
    }
}
