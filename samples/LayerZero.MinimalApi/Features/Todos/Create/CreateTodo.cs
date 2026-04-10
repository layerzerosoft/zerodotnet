using LayerZero.AspNetCore;
using LayerZero.Core;
using LayerZero.MinimalApi.Features.Todos.Get;
using LayerZero.MinimalApi.Infrastructure.Todos;
using Microsoft.AspNetCore.Http.HttpResults;
using CreateTodoRequest = LayerZero.MinimalApi.Contracts.Todos.CreateTodo.Request;
using TodoContract = LayerZero.MinimalApi.Contracts.Todos.Todo;
using TodoRoutes = LayerZero.MinimalApi.Contracts.Todos.TodoRoutes;

namespace LayerZero.MinimalApi.Features.Todos.Create;

public static class CreateTodo
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGroup(TodoRoutes.Base)
            .WithTags("Todos")
            .MapPost("", async Task<Created<TodoContract>> (
                CreateTodoRequest request,
                IAsyncRequestHandler<CreateTodoRequest, TodoContract> handler,
                LinkGenerator links,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .HandleAsync(request, cancellationToken)
                        .ConfigureAwait(false);

                    var location = links.GetPathByName(
                        httpContext,
                        nameof(GetTodo),
                        new { id = result.Value.Id });

                    return TypedResults.Created(location ?? $"/todos/{result.Value.Id}", result.Value);
                })
                .Validate<CreateTodoRequest>()
                .WithName(nameof(CreateTodo))
                .WithSummary("Create a todo")
                .WithDescription("Create a todo and return its canonical resource link.")
                .Produces<TodoContract>(StatusCodes.Status201Created);
    }

    public sealed class Handler(ITodoRepository todos) : IAsyncRequestHandler<CreateTodoRequest, TodoContract>
    {
        private readonly ITodoRepository todos = todos;

        public async ValueTask<Result<TodoContract>> HandleAsync(
            CreateTodoRequest command,
            CancellationToken cancellationToken = default)
        {
            var title = command.Title?.Trim() ?? string.Empty;

            var todo = await todos
                .AddAsync(title, command.DueOn, cancellationToken)
                .ConfigureAwait(false);

            var response = new TodoContract(todo.Id, todo.Title, todo.DueOn, todo.IsCompleted);

            return Result<TodoContract>.Success(response);
        }
    }

    public sealed class Validator : Validation.Validator<CreateTodoRequest>
    {
        public Validator()
        {
            RuleFor(nameof(CreateTodoRequest.Title), request => request.Title)
                .NotEmpty()
                .MaximumLength(120);
        }
    }
}
