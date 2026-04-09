using System.Collections.Concurrent;

namespace LayerZero.MinimalApi.Infrastructure.Todos;

public sealed class InMemoryTodoRepository : ITodoRepository
{
    private readonly ConcurrentDictionary<Guid, TodoItem> todos = new();
    private readonly TimeProvider timeProvider;

    public InMemoryTodoRepository(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;

        TodoItem sample = new(
            Guid.Parse("8a0f8ac8-f090-4f7e-bb0f-a2f0b8df6d63"),
            "Map the first LayerZero slice",
            DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime.AddDays(1)),
            IsCompleted: false,
            timeProvider.GetUtcNow());

        todos.TryAdd(sample.Id, sample);
    }

    public ValueTask<TodoItem> AddAsync(string title, DateOnly? dueOn, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TodoItem todo = new(Guid.NewGuid(), title, dueOn, IsCompleted: false, timeProvider.GetUtcNow());
        todos[todo.Id] = todo;

        return ValueTask.FromResult(todo);
    }

    public ValueTask<TodoItem?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        todos.TryGetValue(id, out TodoItem? todo);

        return ValueTask.FromResult(todo);
    }

    public ValueTask<IReadOnlyList<TodoItem>> ListAsync(bool includeCompleted, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TodoItem[] result = todos.Values
            .Where(todo => includeCompleted || !todo.IsCompleted)
            .OrderBy(todo => todo.CreatedAt)
            .ThenBy(todo => todo.Title, StringComparer.Ordinal)
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<TodoItem>>(result);
    }

    public ValueTask<TodoItem?> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (todos.TryGetValue(id, out TodoItem? existing))
        {
            TodoItem completed = existing with { IsCompleted = true };
            if (todos.TryUpdate(id, completed, existing))
            {
                return ValueTask.FromResult<TodoItem?>(completed);
            }
        }

        return ValueTask.FromResult<TodoItem?>(null);
    }
}
