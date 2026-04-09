using LayerZero.AspNetCore;
using LayerZero.Core;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LayerZero.MinimalApi.Features.Todos.Complete;

public static class CompleteTodo
{
    public const string EndpointName = "Todos_Complete";

    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/todos").WithTags("Todos");

        group.MapPost("/{id:guid}/complete", async Task<Results<Ok<Response>, NotFound>> (
                [AsParameters] Request request,
                ICommandHandler<Command, Response> handler,
                CancellationToken cancellationToken) =>
            {
                Result<Response> result = await handler
                    .HandleAsync(new Command(request.Id), cancellationToken)
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

    public sealed class Request
    {
        [FromRoute]
        public Guid Id { get; init; }
    }

    public sealed record Command(Guid Id) : ICommand<Response>;

    public sealed record Response(Guid Id, string Title, DateOnly? DueOn, bool IsCompleted);

    public sealed class Handler : ICommandHandler<Command, Response>
    {
        private readonly ITodoRepository todos;

        public Handler(ITodoRepository todos)
        {
            this.todos = todos;
        }

        public async ValueTask<Result<Response>> HandleAsync(
            Command command,
            CancellationToken cancellationToken = default)
        {
            TodoItem? todo = await todos
                .CompleteAsync(command.Id, cancellationToken)
                .ConfigureAwait(false);

            if (todo is null)
            {
                return Result<Response>.Failure(Error.Create(
                    "todos.not_found",
                    "Todo was not found.",
                    nameof(Command.Id)));
            }

            return Result<Response>.Success(new Response(todo.Id, todo.Title, todo.DueOn, todo.IsCompleted));
        }
    }
}
