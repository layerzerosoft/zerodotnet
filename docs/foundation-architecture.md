# LayerZero Foundation Architecture

LayerZero is the layer zero for modern .NET services. It should feel small,
sharp, and inevitable: a clear standard for the things every service needs
before teams reach for heavy frameworks.

## Current Milestone

- Core result and error primitives.
- Sync and async vertical-slice handler contracts.
- Command and event message contracts in `LayerZero.Core`.
- Transport-neutral async messaging runtime in `LayerZero.Messaging`.
- First-party RabbitMQ, Azure Service Bus, Kafka, and NATS JetStream adapters.
- Real-broker adapter integration suites plus a fulfillment end-to-end broker matrix.
- Self-mapping Minimal API endpoint slices.
- Source-generated `AddSlices()` and `MapSlices()` as the default discovery path.
- Source-generated `AddMessages()` and compile-time message manifests.
- Request validation through LayerZero validators and endpoint-filter factories.
- ProblemDetails responses with machine-readable `layerzero.errors` metadata.
- First-party testing assertions.
- Source-controlled HTTP contracts and explicit typed clients.
- Fulfillment sample family with API, processing worker, projections worker,
  bootstrap host, typed client, and Aspire AppHost orchestration.
- Dependency policy tests that block commercial-pressure and drift-prone dependencies.

## Slice Mechanics

Slices are first-class citizens. A slice owns one use case and should live near
its request, response, handler, validator, and tests. Feature folders are the
preferred organization for applications and samples because they keep behavior
easy for humans and agents to navigate.

HTTP slices are static modules discovered by convention: a non-generic
`static class` that exposes
`public static void MapEndpoint(IEndpointRouteBuilder endpoints)`.

The primary authoring experience should look like a plain Minimal API module,
not framework ceremony. Manual mapping is the direct static call:

```csharp
CreateTodo.MapEndpoint(app);
```

Inside `MapEndpoint`, use ASP.NET Core directly. LayerZero must not hide
Minimal API mechanics from developers. Slices can call route groups, `MapGet`,
`MapPost`, endpoint filters, authorization, metadata, typed results,
`[AsParameters]`, `HttpContext`, `LinkGenerator`, and any other native Minimal
API feature.

Source generation is the default for performance, AOT, and trimming posture.
The generator discovers concrete slice services at compile time and emits:

```csharp
services.AddSlices();
endpoints.MapSlices();
```

Runtime assembly scanning is not the primary discovery model. Explicit
registration of handlers through normal ASP.NET Core DI and lower-level
`MapGetSlice*` / `MapPostSlice*` helpers remain as escape hatches for unusual
cases.

## Validation

Validators inherit from `Validator<T>` and declare rules with
`RuleFor(name, accessor)`. The API avoids expression-tree reflection in the
foundation so it stays friendlier to AOT and trimming.

ASP.NET Core integration validates request models before handlers run and
returns `application/problem+json` with standard validation errors plus
`layerzero.errors` for code-aware clients and agents.

Endpoint validation uses a filter factory so the request argument index is
resolved once per endpoint. This keeps validation compatible with native
Minimal API signatures while avoiding per-request argument scanning.

`Program` remaining `partial` for test-host wiring is unrelated to slice
design and should not be mirrored by HTTP slice modules.

## Messaging Foundation

Messaging is now implemented as ports first, transports second.

- `LayerZero.Core` still owns the command and event contracts.
- `LayerZero.Messaging` owns transport-neutral runtime behavior such as
  `ICommandSender`, `IEventPublisher`, `MessageContext`,
  `IMessageProcessor`, `IMessageFailureClassifier`, and
  `IMessageIdempotencyStore`.
- `MessageContext` and the generated manifest now carry affinity metadata so
  adapters can project locality concepts such as Kafka keys and Service Bus
  sessions without promising identical FIFO semantics everywhere.
- The generator now emits `AddMessages()`, a compile-time message registry,
  deterministic logical names, and handler invokers for command/event flows.
- Validation and handler execution stay generated and DI-first. Runtime
  assembly scanning remains out of scope.
- Envelope metadata is standardized so adapters can share correlation,
  tracing, timestamp, attempt, and header behavior.
- Commands resolve to one durable consumer path. Events resolve to pub/sub
  entities with durable subscriptions.
- Transport adapters now ship for RabbitMQ, Azure Service Bus, Kafka, and
  NATS JetStream. Future brokers must still live in dedicated adapter packages
  and must not leak broker-specific APIs into the core contracts.
- The sync request/response and async message flows share the same validation,
  result, observability, and testing concepts.
- Startup validation is validate-only by default. Provisioning is explicit
  through bootstrap paths so production startup does not mutate broker
  infrastructure by surprise.
- CI now treats live broker verification as a release gate. RabbitMQ, Kafka,
  NATS, and the Azure Service Bus emulator run in the local matrix, and a
  separate cloud parity workflow covers Azure Service Bus session behavior.

The package-level setup, broker defaults, and fulfillment sample are documented
in `docs/messaging/async-messaging.md`.

## HTTP Clients

HTTP clients are secondary to server authoring, but they are still a first
class part of the foundation.

- `LayerZero.Http` owns shared HTTP contracts: route templates, HTTP methods,
  request and response DTOs, and request metadata such as route, query, header,
  and JSON body bindings.
- `LayerZero.Client` owns runtime client primitives such as `LayerZeroClient`,
  `ApiResponse`, `ApiResponse<T>`, failure normalization into LayerZero
  `Result`, and standard `IHttpClientFactory` registration.
- Typed clients are explicit source-controlled classes. They wrap
  `LayerZeroClient`, reference shared contracts, and stay fully reviewable.
- Shared contracts define HTTP API surface only. They must not absorb
  ASP.NET Core binding attributes, tags, summaries, descriptions, DI services,
  or other server-only concerns.
- Server slices remain native Minimal APIs. They may reuse contract routes and
  DTOs, but they keep full control over binding, validation, filters, auth,
  typed results, and OpenAPI metadata.
- OpenAPI remains documentation and CI artifact infrastructure. It is not the
  client source of truth and must not drive the endpoint authoring model.
- Swashbuckle, NSwag, and Kiota stay outside the foundation.

## Dashboard Roadmap

The dashboard will ship as a NuGet package that serves a React static web app
from ASP.NET Core middleware.

- The middleware owns static asset serving, route fallback, and secure defaults.
- The React app consumes stable JSON endpoints exposed by the ASP.NET Core package.
- Dashboard assets must be versioned with the package and work without a Node.js
  toolchain in consuming applications.
- The dashboard should help humans and AI agents understand slices, routes,
  validators, messages, health, and dependency posture.

## References

- Minimal API route handlers and endpoint organization: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0
- Minimal API parameter binding and `[AsParameters]`: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0
- Typed results and OpenAPI metadata: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0
- Built-in ASP.NET Core OpenAPI: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0
- `IHttpClientFactory`: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-10.0
- Incremental source generators: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator
