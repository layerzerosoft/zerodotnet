using LayerZero.AspNetCore;
using LayerZero.Core;
using LayerZero.MinimalApi.Features.Todos.Get;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LayerZero.MinimalApi.Features.Todos.Create;

public sealed partial class CreateTodo : IEndpointSlice
{
    public const string EndpointName = "Todos_Create";

    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/todos").WithTags("Todos");

        group.MapPost("", async Task<Created<Response>> (
                [FromBody] Request request,
                ICommandHandler<Command, Response> handler,
                LinkGenerator links,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                Result<Response> result = await handler
                    .HandleAsync(new Command(request.Title!.Trim(), request.DueOn), cancellationToken)
                    .ConfigureAwait(false);

                string? location = links.GetPathByName(
                    httpContext,
                    GetTodo.EndpointName,
                    new { id = result.Value.Id });

                return TypedResults.Created(location ?? $"/todos/{result.Value.Id}", result.Value);
            })
            .Validate<Request>()
            .WithName(EndpointName)
            .WithSummary("Create a todo")
            .WithDescription("Create a todo and return its canonical resource link.")
            .Produces<Response>(StatusCodes.Status201Created);
    }

    public sealed record Request(string? Title, DateOnly? DueOn);

    public sealed record Command(string Title, DateOnly? DueOn) : ICommand<Response>;

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
            TodoItem todo = await todos
                .AddAsync(command.Title, command.DueOn, cancellationToken)
                .ConfigureAwait(false);

            Response response = new(todo.Id, todo.Title, todo.DueOn, todo.IsCompleted);
            return Result<Response>.Success(response);
        }
    }

    public sealed class Validator : LayerZero.Validation.Validator<Request>
    {
        public Validator()
        {
            RuleFor(nameof(Request.Title), request => request.Title)
                .NotEmpty()
                .MaximumLength(120);
        }
    }
}
