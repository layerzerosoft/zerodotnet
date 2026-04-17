# LayerZero

LayerZero is a modern, AI-agent-friendly foundation for .NET.

The project starts with ASP.NET Core, Minimal APIs, first-class slices,
source-generated slice registration, validation, OpenAPI, source-controlled
HTTP contracts, explicit typed clients, transport-neutral async messaging,
RabbitMQ, Azure Service Bus, Kafka, and NATS adapters, code-first relational
migrations, first-class seed profiles, and first-party testing primitives. The
React dashboard still sits behind that foundation so it can stay small,
explicit, and reliable instead of turning into framework-sized magic.

## Foundation

- Baseline: .NET 10 LTS, `net10.0`, C# latest.
- Public packages: `LayerZero.Core`, `LayerZero.Validation`,
  `LayerZero.AspNetCore`, `LayerZero.Generators`, `LayerZero.Http`,
  `LayerZero.Data`, `LayerZero.Data.SqlServer`, `LayerZero.Migrations`,
  `LayerZero.Migrations.SqlServer`, `LayerZero.Messaging`, `LayerZero.Messaging.RabbitMq`,
  `LayerZero.Messaging.AzureServiceBus`, `LayerZero.Messaging.Kafka`,
  `LayerZero.Messaging.Nats`, `LayerZero.Testing`, and `LayerZero.Client`.
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
- `LayerZero.Data`: provider-agnostic relational/data foundation, connection abstractions, and typed entity mapping.
- `LayerZero.Data.SqlServer`: SQL Server provider registration and SQL Server data services.
- `LayerZero.Messaging`: transport-neutral command/event dispatch, message envelopes, routing, idempotency hooks, and compile-time message manifests.
- `LayerZero.Messaging.RabbitMq`: RabbitMQ transport defaults, topology validation/provisioning, hosted consumers, and health checks.
- `LayerZero.Messaging.AzureServiceBus`: Azure Service Bus transport defaults, session-aware affinity support, topology validation/provisioning, hosted consumers, and health checks.
- `LayerZero.Messaging.Kafka`: Kafka transport defaults, retry/dead-letter topics, topology validation/provisioning, hosted consumers, and health checks.
- `LayerZero.Messaging.Nats`: NATS JetStream transport defaults, topology validation/provisioning, hosted consumers, and health checks.
- `LayerZero.Migrations`: provider-neutral relational migration runtime, forward-only migration DSL, typed map integration, seed profiles, validation, baselining, app-hosted commands, and internal analyzer-driven discovery.
- `LayerZero.Migrations.SqlServer`: SQL Server migration adapter, SQL generation, schema history storage, `sp_getapplock` runner coordination, and explicit execution flows.
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

## Relational Migrations

LayerZero relational migrations are code-first, forward-only, convention-first,
and data-foundation-first:

```csharp
builder.Services
    .AddLayerZeroData(options =>
    {
        options.ConnectionStringName = "Main";
    })
    .UseSqlServer()
    .UseMigrations(options =>
    {
        options.Executor = "orders-deploy";
    });
```

Migration discovery is shipped with `LayerZero.Migrations` itself, not with
`LayerZero.Generators`, and the default authoring path is convention-based:

- migration files live under `Migrations/` and use names like
  `20260414120000_CreateAccounts.cs`
- seed files live under `Seeds/<profile>/` and use names like
  `20260414121000_Roles.cs`
- ids come from the scaffolded file names, display names come from the type
  names, and artifacts no longer hardcode constructor metadata

```csharp
internal sealed class CreateAccountsMigration : Migration
{
    public override void Build(MigrationBuilder builder)
    {
        builder.CreateTable("accounts", table =>
        {
            table.Column("id").AsGuid().NotNull();
            table.Column("email").AsString(256).NotNull();
            table.PrimaryKey("id");
        });
    }
}
```

The runtime surface stays explicit: `InfoAsync`, `ValidateAsync`, `ScriptAsync`,
`ApplyAsync`, and `BaselineAsync`. Seeds are first-class and profile-aware:
`baseline` is always safe to run, while profiles such as `dev` and `demo` stay
opt-in. Commands are designed to run through the app host and normal .NET
configuration, with CLI connection-string overrides available only as an escape
hatch. The current SQL Server foundation is documented in
`docs/migrations/relational-migrations.md`.

## Async Messaging

LayerZero messaging standardizes:

- generated message discovery and logical names
- transport-neutral `ICommandSender` and `IEventPublisher`
- point-to-point command routing and pub/sub event fan-out
- envelope metadata such as message id, correlation id, causation id, trace
  context, timestamp, attempt, and headers
- validation-aware and result-aware processing through `IMessageProcessor`
- idempotency hooks through `IMessageIdempotencyStore`
- transport adapters for RabbitMQ, Azure Service Bus, Kafka, and NATS JetStream
- topology validation via `IMessageTopologyManager`
- affinity-aware routing metadata through `MessageContext.AffinityKey`

Real-broker verification is part of the repo now. `dotnet test` covers:

- transport-neutral runtime tests
- generator and architecture tests
- RabbitMQ, Azure Service Bus emulator, Kafka, and NATS adapter integration tests
- the fulfillment end-to-end matrix across every local broker profile

The supported fulfillment E2E lanes are:

- fast local inner loop:
  `dotnet test tests/LayerZero.Fulfillment.EndToEnd.Tests/LayerZero.Fulfillment.EndToEnd.Tests.csproj --no-build --filter "Category=LocalFast"`
- full local fulfillment matrix:
  `dotnet test tests/LayerZero.Fulfillment.EndToEnd.Tests/LayerZero.Fulfillment.EndToEnd.Tests.csproj --no-build --filter "Category!=CloudOptional"`
- cloud Azure Service Bus parity:
  `dotnet test LayerZero.slnx --no-build --filter "Category=CloudOptional"`

The fulfillment sample family exercises those defaults with order placement,
inventory, payment, shipment, retries, dead-lettering, duplicate-delivery
protection, and correlation-aware timelines.

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
dotnet format analyzers LayerZero.slnx --diagnostics IDE0005 --verify-no-changes
dotnet format analyzers LayerZero.slnx --diagnostics IDE0005
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet test tests/LayerZero.Fulfillment.EndToEnd.Tests/LayerZero.Fulfillment.EndToEnd.Tests.csproj --no-build --filter "Category=LocalFast"
dotnet test tests/LayerZero.Fulfillment.EndToEnd.Tests/LayerZero.Fulfillment.EndToEnd.Tests.csproj --no-build --filter "Category!=CloudOptional"
dotnet pack --no-build
```

Run the sample:

```bash
dotnet run --project samples/LayerZero.Fulfillment.RabbitMq.AppHost
dotnet run --project samples/LayerZero.Fulfillment.AzureServiceBus.AppHost
dotnet run --project samples/LayerZero.Fulfillment.Kafka.AppHost
dotnet run --project samples/LayerZero.Fulfillment.Nats.AppHost
```

For VS Code, use the checked-in broker-specific Aspire configurations with the
Microsoft Aspire extension and C# Dev Kit. That is the supported distributed-app
debug path for this sample:

- `Aspire: Fulfillment RabbitMQ AppHost`
- `Aspire: Fulfillment Azure Service Bus AppHost`
- `Aspire: Fulfillment Kafka AppHost`
- `Aspire: Fulfillment NATS AppHost`

Each AppHost dashboard uses stable development URLs:

- RabbitMQ: `https://localhost:17134` and `http://localhost:15170`
- Azure Service Bus emulator: `https://localhost:17135` and `http://localhost:15171`
- Kafka: `https://localhost:17136` and `http://localhost:15172`
- NATS JetStream: `https://localhost:17137` and `http://localhost:15173`

Each broker AppHost exposes one API endpoint pair:

- RabbitMQ: `http://localhost:5381` and `https://localhost:7381`
- Azure Service Bus emulator: `http://localhost:5382` and `https://localhost:7382`
- Kafka: `http://localhost:5383` and `https://localhost:7383`
- NATS JetStream: `http://localhost:5384` and `https://localhost:7384`

Those are the stable AppHost proxy URLs. Under normal Aspire behavior the
underlying API process listens on a different allocated loopback port, so API
resource logs can show a different internal port without indicating a problem.

Local orchestration prerequisites:

- Docker Desktop, Podman, or another OCI-compatible container runtime
- .NET 10 SDK
- enough free ports for the selected broker AppHost and its API endpoints

`dotnet test --no-build` expects a live local Docker daemon because the broker
integration suites run against real containers and the Azure Service Bus
emulator. The separate cloud Azure Service Bus parity workflow uses these
secrets:

- `LAYERZERO_AZURE_SERVICE_BUS_CLOUD_CONNECTION_STRING`
- `LAYERZERO_AZURE_SERVICE_BUS_CLOUD_ADMIN_CONNECTION_STRING`

During a full local broker matrix run it is normal to briefly see multiple
containers for the same broker, such as one RabbitMQ instance for transport
integration tests and another for fulfillment end-to-end tests. What is not
normal is those broker containers or their `testcontainers-ryuk` sidecars
lingering after the run completes.

The supported cleanup path for stale local sessions is:

```bash
dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --list
dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --apply --older-than 30m
```

If you still have legacy orphaned sessions from before repo-owned labels were
added, remove them explicitly by session id:

```bash
dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --apply --older-than 0m --session-id <testcontainers-session-id>
```

The cleanup tool only targets repo-owned stale Testcontainers sessions. Local
cleanup stays explicit; CI uses the same tool with a zero-minute threshold in
its final `always()` cleanup step.

The flagship sample family lives under `samples/LayerZero.Fulfillment.*`.
Each broker-specific AppHost provisions local topology through
`LayerZero.Fulfillment.Bootstrap` and starts:

- `LayerZero.Fulfillment.Api`
- `LayerZero.Fulfillment.Processing`
- `LayerZero.Fulfillment.Projections`

The broker-specific AppHosts are:

- `samples/LayerZero.Fulfillment.RabbitMq.AppHost`
- `samples/LayerZero.Fulfillment.AzureServiceBus.AppHost`
- `samples/LayerZero.Fulfillment.Kafka.AppHost`
- `samples/LayerZero.Fulfillment.Nats.AppHost`

AppHost startup stays covered by checked-in launch profiles, checked-in Aspire
VS Code debug configurations, and launch-settings policy tests.

The AppHost dashboard also adds direct OpenAPI links for the fulfillment API so
you can open `/openapi/v1.json` from the resource card without manually editing
the base URL.

Each broker AppHost shares one SQLite database file across bootstrap, API,
processing, and projections under its own `data/` directory.

The API sample launch profiles use stable dev URLs:

- HTTP: `http://localhost:5380`
- HTTPS: `https://localhost:7380`

Then open `/openapi/v1.json`, `POST /orders`, `GET /orders/{id}`, or
`GET /orders/{id}/timeline`.

If the AppHost HTTPS profile cannot start because the local development
certificate is missing, trust the standard .NET dev certificate:

```bash
dotnet dev-certs https --trust
```

Launching the AppHost without a launch profile is not a supported local-dev
path. Official Aspire AppHost startup depends on the checked-in launch profile
or an equivalent debugger configuration that supplies the dashboard and resource
service endpoints.

When switching between broker AppHosts, start from a clean local session. Stale
standalone `LayerZero.Fulfillment.*` processes can make port and log
observations look contradictory even when the active AppHost is healthy.

In VS Code, the editor Run or Debug button on an arbitrary open `.cs` file
launches the project associated with that file. It does not automatically
launch the AppHost unless the selected debug configuration is one of the
broker-specific `Aspire: Fulfillment ... AppHost` entries.

The supported VS Code AppHost debugger type is `aspire`, not `dotnet`. If VS
Code stops on a user-unhandled framework exception while debugging the
distributed app, judge resource health by the Aspire dashboard resource state
and resource logs rather than by the debugger stop alone.

Run the typed client sample against the API:

```bash
dotnet run --project samples/LayerZero.Fulfillment.Client -- https://localhost:7380
```

Messaging docs:

- hub: [`docs/messaging/async-messaging.md`](docs/messaging/async-messaging.md)
- fulfillment runbook: [`docs/messaging/fulfillment-sample.md`](docs/messaging/fulfillment-sample.md)
- RabbitMQ: [`docs/messaging/rabbitmq.md`](docs/messaging/rabbitmq.md)
- Azure Service Bus: [`docs/messaging/azure-service-bus.md`](docs/messaging/azure-service-bus.md)
- Kafka: [`docs/messaging/kafka.md`](docs/messaging/kafka.md)
- NATS: [`docs/messaging/nats.md`](docs/messaging/nats.md)

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
