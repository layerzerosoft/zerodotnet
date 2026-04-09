using LayerZero.AspNetCore;
using LayerZero.Core;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LayerZero.MinimalApi.Features.Todos.List;

public sealed partial class ListTodos : IEndpointSlice
{
    public const string EndpointName = "Todos_List";

    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/todos").WithTags("Todos");

        group.MapGet("", async Task<Ok<IReadOnlyList<Response>>> (
                [AsParameters] Request request,
                IAsyncRequestHandler<Request, IReadOnlyList<Response>> handler,
                CancellationToken cancellationToken) =>
            {
                Result<IReadOnlyList<Response>> result = await handler
                    .HandleAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                return TypedResults.Ok(result.Value);
            })
            .WithName(EndpointName)
            .WithSummary("List todos")
            .WithDescription("List active todos by default, or include completed todos with a query flag.");
    }

    public sealed class Request
    {
        [FromQuery]
        public bool IncludeCompleted { get; init; }

        [FromServices]
        public ITodoRepository Todos { get; init; } = null!;
    }

    public sealed record Response(Guid Id, string Title, DateOnly? DueOn, bool IsCompleted);

    public sealed class Handler : IAsyncRequestHandler<Request, IReadOnlyList<Response>>
    {
        public async ValueTask<Result<IReadOnlyList<Response>>> HandleAsync(
            Request request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TodoItem> todos = await request.Todos
                .ListAsync(request.IncludeCompleted, cancellationToken)
                .ConfigureAwait(false);

            Response[] response = todos
                .Select(todo => new Response(todo.Id, todo.Title, todo.DueOn, todo.IsCompleted))
                .ToArray();

            return Result<IReadOnlyList<Response>>.Success(response);
        }
    }
}
