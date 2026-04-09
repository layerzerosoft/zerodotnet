# ZeroDotNet Agent Rulebook

This file is the canonical instruction source for every AI agent working in
this repository. Other agent files must stay thin and point back here.

## Mission

Build modern and revolutionary standardised steroids for the .NET community:
small primitives, sharp APIs, excellent defaults, and first-class agent
collaboration.

ZeroDotNet should feel bold without becoming chaotic. Favor simple contracts,
clear package boundaries, and boringly reliable builds.

## Project Identity

- Public product name: ZeroDotNet.
- Public package prefix: `LayerZero.ZeroDotNet.*`.
- Legal and repository owner: `layerzerosoft`.
- License: MIT.
- Baseline framework: .NET 10 LTS, `net10.0`.

## Engineering Rules

- Keep the foundation dependency-light.
- Do not add MassTransit, MediatR/Mediator, FluentValidation, FluentAssertions,
  Swashbuckle, NSwag, or EF Core without an explicit architecture decision.
- Prefer Microsoft built-in ASP.NET Core and OpenAPI primitives.
- Treat Minimal APIs and vertical slices as first-class citizens.
- Make sync and async flows equally intentional.
- Keep public APIs AOT-aware and trimming-aware. Avoid reflection unless there
  is a strong documented reason.
- Prefer small composable abstractions over framework-sized magic.
- Add tests with behavior-level names and failure messages that help agents act.
- Keep package boundaries clean: core has no ASP.NET Core dependency, broker
  SDKs live only in future transport adapters.

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

## Future Architecture Guardrails

Messaging starts with ports, then transport adapters. Dashboard starts with
middleware and stable JSON endpoints, then a packaged React static app. Do not
stub broad packages just to look complete; add packages when their contracts are
clear enough to test.
