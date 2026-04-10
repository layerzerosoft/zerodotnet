# LayerZero

LayerZero is a modern, AI-agent-friendly foundation for .NET.

The project starts with ASP.NET Core, Minimal APIs, first-class slices,
source-generated slice registration, validation, OpenAPI, source-controlled
HTTP contracts, explicit typed clients, transport-neutral async messaging,
and first-party testing primitives. Broker-specific adapters and the React
dashboard still sit behind that foundation so they can stay small, explicit,
and reliable instead of turning into framework-sized magic.

## Foundation

- Baseline: .NET 10 LTS, `net10.0`, C# latest.
- Public packages: `LayerZero.Core`, `LayerZero.Validation`,
  `LayerZero.AspNetCore`, `LayerZero.Generators`, `LayerZero.Http`,
  `LayerZero.Messaging`, `LayerZero.Testing`, and `LayerZero.Client`.
- Legal and repository owner: `layerzerosoft`.
- API posture: dependency-light, source-generator-first, AOT-aware, trimming-aware, Minimal API native.
- OpenAPI posture: Microsoft built-in `Microsoft.AspNetCore.OpenApi`, OpenAPI 3.1.
- Excluded from foundation dependencies: MassTransit, MediatR/Mediator,
  FluentValidation, FluentAssertions, Shouldly, AwesomeAssertions, Swashbuckle,
  NSwag, EF Core, and framework-level messaging stacks. Broker SDKs stay
  confined to dedicated adapter packages.

## Packages

- `LayerZero.Core`: result/error primitives, sync/async request contracts, and command/event message contracts.
- `LayerZero.Validation`: fluent validation rules for Minimal API request models.
- `LayerZero.AspNetCore`: self-mapping endpoint slices, explicit registration escape hatches, endpoint filters, and ProblemDetails integration.
- `LayerZero.Generators`: compile-time slice discovery for `AddSlices()` and `MapSlices()`.
- `LayerZero.Http`: source-controlled HTTP contracts shared by servers and clients.
- `LayerZero.Messaging`: transport-neutral command/event dispatch, message envelopes, routing, idempotency hooks, and compile-time message manifests.
- `LayerZero.Testing`: fluent first-party assertions for LayerZero result and validation flows.
- `LayerZero.Client`: `LayerZeroClient`, `ApiResponse`,
  `Result`-first failure mapping, and `IHttpClientFactory` registration for explicit typed clients.

## Slice Model

Slices are the primary programming model. HTTP slices are static modules
discovered by convention: a non-generic `static class` with
`public static void MapEndpoint(IEndpointRouteBuilder)`. Inside that method,
developers use native Minimal API features directly:
`MapGet`, `MapPost`, route groups, filters, auth, metadata, `[AsParameters]`,
typed results, `HttpContext`, `LinkGenerator`, and OpenAPI conventions.

The default path is generated:

```csharp
builder.Services.AddLayerZero().AddSlices();
app.MapSlices();
```

Manual mapping is the direct static call:

```csharp
CreateTodo.MapEndpoint(app);
```

`AddValidator<TRequest, TValidator>()`, `MapGetSlice*`, and `MapPostSlice*`
remain lower-level escape hatches. Runtime assembly scanning is not the default
discovery model.

Non-HTTP slices start with command and event contracts in `LayerZero.Core`,
then opt into the transport-neutral messaging runtime in
`LayerZero.Messaging`. The default generated path is:

```csharp
builder.Services
    .AddMessaging()
    .AddMessages();
```

`AddMessages()` is generated at compile time alongside `AddSlices()`. It
registers discovered validators, command handlers, event handlers, a message
registry, and handler invokers without runtime assembly scanning.

The current messaging foundation is documented in
`docs/messaging/async-messaging.md`.

## Async Messaging

LayerZero messaging standardizes:

- generated message discovery and logical names
- transport-neutral `ICommandSender` and `IEventPublisher`
- envelope metadata such as message id, correlation id, causation id, trace
  context, timestamp, attempt, and headers
- validation-aware and result-aware processing through `IMessageProcessor`
- idempotency hooks through `IMessageIdempotencyStore`

Dedicated RabbitMQ, Azure Service Bus, Kafka, and NATS transport adapters are
the next layer on top of this foundation.

## HTTP Clients

LayerZero clients stay secondary to native Minimal API authoring. Server code
keeps using built-in ASP.NET Core OpenAPI and normal Minimal API metadata, but
client consumption is driven by shared HTTP contracts, not by OpenAPI codegen.

Shared contracts project setup:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\LayerZero.Http\LayerZero.Http.csproj" />
</ItemGroup>
```

Example contract:

```csharp
public static class GetTodo
{
    public static readonly GetEndpoint<Request, Todo> Endpoint = HttpEndpoint
        .Get<Request, Todo>(TodoRoutes.Resource)
        .Route("id", static request => request.Id);

    public sealed record Request(Guid Id);
}
```

Typed client registration and usage:

```csharp
services.AddLayerZeroClient<TodosClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7270");
});

Result<IReadOnlyList<Todo>> todos =
    await todosClient.ListAsync(includeCompleted: true, cancellationToken);
```

The exact edit/build/use loop is documented in
`docs/client-dev-experience.md`.

## Commands

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet pack --no-build
```

Run the sample:

```bash
dotnet run --project samples/LayerZero.MinimalApi
```

Sample launch profiles use stable dev URLs:

- HTTP: `http://localhost:5270`
- HTTPS: `https://localhost:7270`

Then open `/openapi/v1.json`, `GET /todos`, or `POST /todos`.

Run the client sample against the API:

```bash
dotnet run --project samples/LayerZero.MinimalApi.Client -- https://localhost:7270
```

## References

- Minimal API route handlers and endpoint organization: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0
- Minimal API parameter binding and `[AsParameters]`: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0
- Typed results and OpenAPI metadata: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0
- Built-in ASP.NET Core OpenAPI: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0
- `IHttpClientFactory`: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-10.0

## Agent Rules

`AGENTS.md` is the canonical rulebook for Codex, Claude, Gemini, Cursor,
Copilot, and future agents. Thin adapter files point back to it so the project
has one source of truth.

Installed Codex skills become active after restarting Codex.
