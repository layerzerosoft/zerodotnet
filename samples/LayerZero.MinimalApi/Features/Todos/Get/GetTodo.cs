using LayerZero.AspNetCore;
using LayerZero.Core;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LayerZero.MinimalApi.Features.Todos.Get;

public static class GetTodo
{
    public const string EndpointName = "Todos_Get";

    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/todos").WithTags("Todos");

        group.MapGet("/{id:guid}", async Task<Results<Ok<Response>, NotFound>> (
                [AsParameters] Request request,
                IAsyncRequestHandler<Request, Response> handler,
                CancellationToken cancellationToken) =>
            {
                Result<Response> result = await handler
                    .HandleAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(result.Value);
            })
            .WithName(EndpointName)
            .WithSummary("Get a todo")
            .WithDescription("Return one todo by id.");
    }

    public sealed class Request
    {
        [FromRoute]
        public Guid Id { get; init; }

        [FromServices]
        public ITodoRepository Todos { get; init; } = null!;
    }

    public sealed record Response(Guid Id, string Title, DateOnly? DueOn, bool IsCompleted);

    public sealed class Handler : IAsyncRequestHandler<Request, Response>
    {
        public async ValueTask<Result<Response>> HandleAsync(
            Request request,
            CancellationToken cancellationToken = default)
        {
            TodoItem? todo = await request.Todos
                .FindAsync(request.Id, cancellationToken)
                .ConfigureAwait(false);

            if (todo is null)
            {
                return Result<Response>.Failure(Error.Create(
                    "todos.not_found",
                    "Todo was not found.",
                    nameof(Request.Id)));
            }

            return Result<Response>.Success(new Response(todo.Id, todo.Title, todo.DueOn, todo.IsCompleted));
        }
    }
}
