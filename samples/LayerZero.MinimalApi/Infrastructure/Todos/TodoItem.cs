namespace LayerZero.MinimalApi.Infrastructure.Todos;

public sealed record TodoItem(
    Guid Id,
    string Title,
    DateOnly? DueOn,
    bool IsCompleted,
    DateTimeOffset CreatedAt);
