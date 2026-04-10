namespace LayerZero.MinimalApi.Contracts.Todos;

public static class TodoRoutes
{
    public const string Base = "/todos";

    public const string ById = "{id:guid}";

    public const string Complete = "{id:guid}/complete";

    public const string Resource = Base + "/{id:guid}";

    public const string Completion = Base + "/{id:guid}/complete";
}
