# LayerZero Async Messaging

`LayerZero.Messaging` is the transport-neutral foundation for LayerZero async
flows.

## What Exists Today

- `ICommandSender` for broker-backed `ICommand` dispatch.
- `IEventPublisher` for broker-backed `IEvent` publication.
- `MessageContext` for standardized envelope metadata.
- `IMessageProcessor` for validation-first, result-aware message handling.
- `IMessageFailureClassifier` for retry vs dead-letter decisions.
- `IMessageIdempotencyStore` for opt-in handler idempotency.
- `AddMessaging()` for runtime services and `AddMessages()` for generated
  registrations and message manifests.

## Generated Model

The source generator now emits:

- discovered validator and handler registrations for non-HTTP workers
- a compile-time message registry
- deterministic logical message names
- generated message invokers that call validators and handlers without runtime
  assembly scanning
- diagnostics for duplicate command handlers and duplicate logical message
  names

Typical setup:

```csharp
builder.Services
    .AddMessaging(options =>
    {
        options.ApplicationName = "todos-worker";
    })
    .AddMessages();
```

## Envelope Defaults

LayerZero message envelopes include:

- `messageId`
- `messageName`
- `messageKind`
- `correlationId`
- `causationId`
- `traceParent`
- `traceState`
- `timestamp`
- `attempt`
- arbitrary headers
- the serialized payload

Validation failures and `Result.Failure(...)` are terminal by default.
Exceptions are retryable by default.

## Current Scope

This repository now ships the async messaging foundation, generator support,
and tests around discovery, routing defaults, serialization, startup
validation, and processing behavior.

Broker-specific transport adapters are intentionally the next step. They
should build on this package instead of introducing a second programming model
or runtime scanning path.
