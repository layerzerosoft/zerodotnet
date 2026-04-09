namespace LayerZero.MinimalApi.Infrastructure.Todos;

public interface ITodoRepository
{
    ValueTask<TodoItem> AddAsync(string title, DateOnly? dueOn, CancellationToken cancellationToken = default);

    ValueTask<TodoItem?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<TodoItem>> ListAsync(bool includeCompleted, CancellationToken cancellationToken = default);

    ValueTask<TodoItem?> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
}
