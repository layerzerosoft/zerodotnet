# LayerZero

LayerZero is a modern, AI-agent-friendly foundation for .NET.

The project starts with ASP.NET Core, Minimal APIs, vertical slices, validation,
OpenAPI, and first-party testing primitives. Message broker adapters and the
React dashboard are intentionally deferred until the foundation is sharp enough
to carry them without drift.

## Foundation

- Baseline: .NET 10 LTS, `net10.0`, C# latest.
- Public packages: `LayerZero.Core`, `LayerZero.Validation`, `LayerZero.AspNetCore`, and `LayerZero.Testing`.
- Legal and repository owner: `layerzerosoft`.
- API posture: dependency-light, AOT-aware, trimming-aware, Minimal API native.
- OpenAPI posture: Microsoft built-in `Microsoft.AspNetCore.OpenApi`, OpenAPI 3.1.
- Excluded from foundation dependencies: MassTransit, MediatR/Mediator,
  FluentValidation, FluentAssertions, Swashbuckle, NSwag, and EF Core.

## Packages

- `LayerZero.Core`: result/error primitives and sync/async vertical-slice contracts.
- `LayerZero.Validation`: fluent validation rules for Minimal API request models.
- `LayerZero.AspNetCore`: Minimal API registration, endpoint mapping, endpoint filters, and ProblemDetails integration.
- `LayerZero.Testing`: fluent first-party assertions for LayerZero result and validation flows.

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

Then open `/openapi/v1.json`, `/pulse`, or `POST /widgets`.

## Agent Rules

`AGENTS.md` is the canonical rulebook for Codex, Claude, Gemini, Cursor,
Copilot, and future agents. Thin adapter files point back to it so the project
has one source of truth.

Installed Codex skills become active after restarting Codex.
