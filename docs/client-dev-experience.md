# LayerZero HTTP Client Developer Experience

This is the exact day-to-day flow for LayerZero HTTP clients.

The client story is intentionally boring in the best way:

- shared contracts are source-controlled
- server slices stay native Minimal APIs
- clients are explicit source-controlled classes
- `HttpClientFactory` stays the transport foundation
- OpenAPI is documentation and CI artifact support, not the client source of truth

## 1. Define a Shared Contracts Project

Create a contracts project that references `LayerZero.Http` and nothing from
ASP.NET Core:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\LayerZero.Http\LayerZero.Http.csproj" />
</ItemGroup>
```

Define plain DTOs plus HTTP surface metadata:

```csharp
using LayerZero.Http;

namespace MyCompany.Todos.Contracts;

public static class TodoRoutes
{
    public const string Base = "/todos";
    public const string Resource = Base + "/{id:guid}";
}

public sealed record Todo(Guid Id, string Title, DateOnly? DueOn, bool IsCompleted);

public static class GetTodo
{
    public static readonly GetEndpoint<Request, Todo> Endpoint = HttpEndpoint
        .Get<Request, Todo>(TodoRoutes.Resource)
        .Route("id", static request => request.Id);

    public sealed record Request(Guid Id);
}
```

Contracts should only describe the HTTP API surface:

- route templates
- HTTP methods
- route/query/header/body bindings
- request and response DTOs

Contracts should not contain:

- ASP.NET Core attributes
- tags, summaries, or descriptions
- DI services
- validation filters
- server-only handler concerns

## 2. Keep the API Native

Server slices stay normal Minimal API code. They may reuse contract paths and
DTOs, but they still own Minimal API behavior and OpenAPI metadata:

```csharp
using Contracts = MyCompany.Todos.Contracts.GetTodo;

group.MapGet("{id:guid}", async Task<Results<Ok<Todo>, NotFound>> (
        Guid id,
        IAsyncRequestHandler<Contracts.Request, Todo> handler,
        CancellationToken cancellationToken) =>
    {
        Result<Todo> result = await handler.HandleAsync(new Contracts.Request(id), cancellationToken);
        return result.IsFailure ? TypedResults.NotFound() : TypedResults.Ok(result.Value);
    })
    .WithName("Todos_Get")
    .WithTags("Todos")
    .WithSummary("Get a todo")
    .WithDescription("Return one todo by id.");
```

Nothing about the client changes how endpoints are authored.

Developers still control:

- `MapGet`, `MapPost`, route groups, filters, auth, typed results
- `WithName`, tags, summaries, descriptions, and other OpenAPI metadata
- validation and handler composition
- ASP.NET Core binding details

## 3. Build an Explicit Typed Client

Typed clients are ordinary classes in source control. They wrap
`LayerZeroClient` and delegate to shared contracts:

```csharp
using System.Text.Json.Serialization;
using LayerZero.Client;
using LayerZero.Core;
using MyCompany.Todos.Contracts;

public sealed class TodosClient
{
    private readonly LayerZeroClient client;

    public TodosClient(HttpClient httpClient)
    {
        client = new LayerZeroClient(httpClient, TodosJsonContext.Default);
    }

    public ValueTask<Result<IReadOnlyList<Todo>>> ListAsync(
        bool? includeCompleted = null,
        CancellationToken cancellationToken = default)
    {
        return client.SendAsync(ListTodos.Endpoint, new ListTodos.Request(includeCompleted), cancellationToken);
    }

    public ValueTask<Result<Todo>> CreateAsync(
        CreateTodo.Request request,
        CancellationToken cancellationToken = default)
    {
        return client.SendAsync(CreateTodo.Endpoint, request, cancellationToken);
    }
}

internal sealed partial class TodosJsonContext : JsonSerializerContext
{
}
```

That is the full client authoring model:

- no code generator
- no hidden `obj` API surface
- no checked-in generated files
- no route literals in consumers

## 4. Register with `HttpClientFactory`

LayerZero stays on the standard .NET path:

```csharp
services.AddLayerZeroClient<TodosClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7270");
});
```

The extension returns `IHttpClientBuilder`, so standard production composition
still works:

```csharp
services.AddLayerZeroClient<TodosClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7270");
})
.AddHttpMessageHandler<AuthHandler>();
```

LayerZero does not replace `HttpClientFactory`, resilience handlers, auth
handlers, or normal .NET networking practices.

## 5. Use the Client

Callers consume a normal typed class:

```csharp
TodosClient todos = provider.GetRequiredService<TodosClient>();

Result<IReadOnlyList<Todo>> listed =
    await todos.ListAsync(includeCompleted: true, cancellationToken);

Result<Todo> created =
    await todos.CreateAsync(
        new CreateTodo.Request("Ship LayerZero", DateOnly.FromDateTime(DateTime.UtcNow)),
        cancellationToken);
```

Advanced callers can ask for `ApiResponse<T>` when they need status codes,
headers, or the parsed problem payload:

```csharp
ApiResponse<Todo> response =
    await todos.CreateForResponseAsync(request, cancellationToken);
```

## 6. Default Runtime Behavior

`LayerZeroClient` uses strong defaults:

- route/query/header formatting uses invariant rules
- nullable query and header values are omitted
- `application/problem+json` plus `layerzero.errors` maps to failed `Result`s
- normal API failures like `400` and `404` do not throw by default
- transport failures such as DNS, TLS, and timeouts stay native
  `HttpRequestException` and `OperationCanceledException`

Compile-time safety comes from the contract types:

- callers do not choose HTTP verbs manually
- the contract chooses the verb
- `GetEndpoint` and `DeleteEndpoint` do not expose `JsonBody(...)`
- body-capable contracts opt into JSON body bindings explicitly

## 7. CI/CD and Packaging

The recommended enterprise flow is:

1. version and publish shared contracts as a NuGet package when teams are split
2. keep typed clients in source control in each consuming service or app
3. run normal `dotnet restore`, `dotnet build`, `dotnet test`, and `dotnet pack`
4. optionally export OpenAPI from the API project as a documentation artifact

OpenAPI remains useful for:

- human-readable API documentation
- CI artifact publishing
- compatibility checks in pipelines

It is not required to build or consume LayerZero clients.

## 8. What Does Not Happen

- no codegen CLI
- no Roslyn client generator
- no public API hidden in `obj`
- no checked-in generated SDK code
- no client-specific server annotations
- no alternate endpoint authoring model
- no hardcoded route literals in consumers when shared contracts exist
