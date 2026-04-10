using System.Text.Json.Serialization;
using LayerZero.MinimalApi.Contracts.Todos;

namespace LayerZero.MinimalApi.Client.Sample.Clients;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Todo))]
[JsonSerializable(typeof(CreateTodo.Request))]
[JsonSerializable(typeof(IReadOnlyList<Todo>))]
internal sealed partial class TodosJsonContext : JsonSerializerContext
{
}
