# LayerZero Agent Rulebook

This file is the canonical instruction source for every AI agent working in
this repository. Other agent files must stay thin and point back here.

## Mission

Build modern and revolutionary standardised steroids for the .NET community:
small primitives, sharp APIs, excellent defaults, and first-class agent
collaboration.

LayerZero should feel bold without becoming chaotic. Favor simple contracts,
clear package boundaries, and boringly reliable builds.

## Project Identity

- Public product name: LayerZero.
- Public packages: `LayerZero.Core`, `LayerZero.Validation`,
  `LayerZero.AspNetCore`, `LayerZero.Generators`, `LayerZero.Http`,
  `LayerZero.Messaging`, `LayerZero.Messaging.RabbitMq`,
  `LayerZero.Messaging.AzureServiceBus`, `LayerZero.Messaging.Kafka`,
  `LayerZero.Messaging.Nats`, `LayerZero.Testing`, and `LayerZero.Client`.
- Legal and repository owner: `layerzerosoft`.
- License: MIT.
- Baseline framework: .NET 10 LTS, `net10.0`.

## Engineering Rules

- Keep the foundation dependency-light.
- Do not add MassTransit, MediatR/Mediator, FluentValidation, FluentAssertions,
  Shouldly, AwesomeAssertions, Swashbuckle, NSwag, Microsoft.Kiota, EF Core,
  broker SDKs, or transport frameworks without an explicit architecture
  decision.
- Prefer Microsoft built-in ASP.NET Core and OpenAPI primitives.
- Treat Minimal APIs and slices as first-class citizens.
- Source generation is the default slice discovery model for performance,
  AOT, and trimming posture. Do not introduce runtime reflection assembly
  scanning as the default path.
- HTTP slices are static modules discovered by `public static void MapEndpoint`.
  Do not use interface-based HTTP slice markers or partial HTTP slice modules.
- HTTP slices must preserve native Minimal API control. Developers should still
  use route groups, `MapGet`, `MapPost`, filters, auth, metadata,
  `[AsParameters]`, typed results, `HttpContext`, `LinkGenerator`, and OpenAPI
  conventions directly.
- Prefer feature folders for application and sample slices: one use case near
  its request, response, handler, validator, messages, and tests.
- Sample and app launch profiles must use explicit non-zero development ports.
  Do not use `localhost:0` as the default developer startup experience. If
  dynamic ports are ever required, use loopback IPs only by explicit design.
- Non-HTTP slices start as command/event contracts in `LayerZero.Core`, then
  compose through `LayerZero.Messaging` and broker adapters. Do not add a
  hidden in-memory bus, outbox/inbox, broker request/reply, or a second
  competing messaging model without an explicit architecture decision.
- Make sync and async flows equally intentional.
- Keep public APIs AOT-aware and trimming-aware. Avoid reflection unless there
  is a strong documented reason.
- Prefer small composable abstractions over framework-sized magic.
- Add tests with behavior-level names and failure messages that help agents act.
- Keep package boundaries clean: core has no ASP.NET Core dependency, broker
  SDKs live only in dedicated transport adapter packages, samples, and
  adapter-focused tests.
- Keep HTTP contracts and HTTP clients secondary to native Minimal API
  authoring. Do not introduce LayerZero-specific server DSLs, client-generation
  attributes, or alternate endpoint models just to improve clients.
- Shared HTTP contracts should describe HTTP API surface only: methods, route
  templates, request and response DTOs, and route/query/header/body bindings.
  They must not own tags, summaries, descriptions, or other OpenAPI metadata.
- Typed clients should be explicit source-controlled classes built on
  `LayerZeroClient` and normal `IHttpClientFactory` composition.
- Prefer LayerZero `Result` and `ApiResponse` surfaces for normal API failures.
  Do not default clients to exception-heavy API behavior.
- Do not reintroduce retired project codenames or standalone framework prefixes
  from earlier naming passes. Public symbols should be concise names like
  `Result`, `Error`, `Validator<T>`, `IRequestHandler<TRequest, TResponse>`,
  and `MapPostSlice`.
- Use `LayerZero` for brand and package identity only. Use `layerzero.*` for
  wire-level error codes and ProblemDetails extension keys.

## Code Style

- Use file-scoped namespaces.
- Use nullable reference types and implicit usings.
- Keep XML docs on public package APIs.
- Avoid marketing copy inside code comments. Comments should explain tradeoffs
  or non-obvious behavior.
- Prefer explicit names over clever abbreviations.

## Verification

Never run `dotnet` commands in a sandboxed environment for this repository.
They can hang without useful output. Always request the required approval and
run `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet pack`, `dotnet run`,
and related .NET CLI commands outside the sandbox.

Run these before handing off changes:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet pack --no-build
```

If a command cannot run, report the exact blocker.

## Architecture Guardrails

Messaging is now ports first, adapters second. Keep broker-specific behavior in
`LayerZero.Messaging.*` packages and keep transport-neutral contracts in
`LayerZero.Core` and `LayerZero.Messaging`. Do not stub broad packages just to
look complete; add new packages when their contracts are clear enough to test.

The dashboard still starts with middleware and stable JSON endpoints, then a
packaged React static app.

OpenAPI is for documentation and CI artifact flows. The client story is shared
contracts plus explicit typed clients, not hidden generated code in `obj`.
