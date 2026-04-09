# ZeroDotNet Foundation Architecture

ZeroDotNet is the layer zero for modern .NET services. It should feel small,
sharp, and inevitable: a clear standard for the things every service needs
before teams reach for heavy frameworks.

## Current Milestone

- Core result and error primitives.
- Sync and async vertical-slice handler contracts.
- Minimal API endpoint mapping for GET and POST slices.
- Request validation through ZeroDotNet validators and endpoint filters.
- ProblemDetails responses with machine-readable `zero.errors` metadata.
- First-party testing assertions.
- Dependency policy tests that block commercial-pressure and drift-prone dependencies.

## Validation

Validators inherit from `ZeroValidator<T>` and declare rules with
`RuleFor(name, accessor)`. The API avoids expression-tree reflection in the
foundation so it stays friendlier to AOT and trimming.

ASP.NET Core integration validates request models before handlers run and
returns `application/problem+json` with standard validation errors plus
`zero.errors` for code-aware clients and agents.

## Messaging Roadmap

Messaging will be introduced as ports first, transports second.

- Core abstractions must describe commands, events, envelopes, metadata,
  retry hints, and idempotency without referencing broker SDKs.
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
