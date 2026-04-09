using LayerZero.Core;

namespace LayerZero.MinimalApi.Features.Todos.Events;

public sealed record TodoCreated(Guid TodoId, string Title, DateTimeOffset CreatedAt) : IEvent;

public sealed class TodoCreatedAuditHandler : IEventHandler<TodoCreated>
{
    public ValueTask<Result> HandleAsync(TodoCreated message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Success());
    }
}
