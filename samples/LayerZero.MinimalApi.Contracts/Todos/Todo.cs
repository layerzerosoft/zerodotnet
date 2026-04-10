namespace LayerZero.MinimalApi.Contracts.Todos;

public sealed record Todo(Guid Id, string Title, DateOnly? DueOn, bool IsCompleted);
