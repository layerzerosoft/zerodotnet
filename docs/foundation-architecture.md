# LayerZero Foundation Architecture

LayerZero is the layer zero for modern .NET services. It should feel small,
sharp, and inevitable: a clear standard for the things every service needs
before teams reach for heavy frameworks.

## Current Milestone

- Core result and error primitives.
- Sync and async vertical-slice handler contracts.
- Command and event message contracts without dispatch or broker behavior.
- Self-mapping Minimal API endpoint slices.
- Source-generated `AddSlices()` and `MapSlices()` as the default discovery path.
- Request validation through LayerZero validators and endpoint-filter factories.
- ProblemDetails responses with machine-readable `layerzero.errors` metadata.
- First-party testing assertions.
- Dependency policy tests that block commercial-pressure and drift-prone dependencies.

## Slice Mechanics

Slices are first-class citizens. A slice owns one use case and should live near
its request, response, handler, validator, and tests. Feature folders are the
preferred organization for applications and samples because they keep behavior
easy for humans and agents to navigate.

HTTP slices implement `IEndpointSlice` and provide
`static void MapEndpoint(IEndpointRouteBuilder endpoints)`. The type itself is a
non-abstract class because C# requires that shape for static abstract interface
members; the entry point is static and the slice does not need runtime
instantiation.

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
registration methods remain as escape hatches for unusual cases.

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

## Messaging Roadmap

Messaging will be introduced as ports first, transports second.

- Core abstractions currently describe commands and events without referencing
  broker SDKs.
- Future contracts may add envelopes, metadata, retry hints, and idempotency
  only after an explicit architecture decision.
- RabbitMQ, Azure Service Bus, Kafka, and other transports must live in
  dedicated adapter packages.
- Transport adapters must not leak broker-specific APIs into the core contracts.
- The sync request/response and async message flows must share validation,
  observability, error, and testing concepts.

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
- Incremental source generators: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator
