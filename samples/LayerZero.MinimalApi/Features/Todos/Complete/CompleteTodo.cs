using LayerZero.Core;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.AspNetCore.Http.HttpResults;
using CompleteTodoRequest = LayerZero.MinimalApi.Contracts.Todos.CompleteTodo.Request;
using TodoContract = LayerZero.MinimalApi.Contracts.Todos.Todo;
using TodoRoutes = LayerZero.MinimalApi.Contracts.Todos.TodoRoutes;

namespace LayerZero.MinimalApi.Features.Todos.Complete;

public static class CompleteTodo
{
    public const string EndpointName = "Todos_Complete";

    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup(TodoRoutes.Base)
            .WithTags("Todos")
            .MapPost(TodoRoutes.Complete, async Task<Results<Ok<TodoContract>, NotFound>> (
                Guid id,
                IAsyncRequestHandler<CompleteTodoRequest, TodoContract> handler,
                CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .HandleAsync(new CompleteTodoRequest(id), cancellationToken)
                        .ConfigureAwait(false);

                    if (result.IsFailure)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(result.Value);
                })
                .WithName(EndpointName)
                .WithSummary("Complete a todo")
                .WithDescription("Mark a todo as complete.");
    }

    public sealed class Handler(ITodoRepository todos) : IAsyncRequestHandler<CompleteTodoRequest, TodoContract>
    {
        public async ValueTask<Result<TodoContract>> HandleAsync(
            CompleteTodoRequest command,
            CancellationToken cancellationToken = default)
        {
            var todo = await todos
                .CompleteAsync(command.Id, cancellationToken)
                .ConfigureAwait(false);

            if (todo is null)
            {
                return Result<TodoContract>.Failure(Error.Create(
                    "todos.not_found",
                    "Todo was not found.",
                    nameof(CompleteTodoRequest.Id)));
            }

            return Result<TodoContract>.Success(new TodoContract(todo.Id, todo.Title, todo.DueOn, todo.IsCompleted));
        }
    }
}
