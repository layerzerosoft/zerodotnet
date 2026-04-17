# Fulfillment Sample Runbook

The flagship async sample lives under `samples/LayerZero.Fulfillment.*` and is
the reference workflow for LayerZero messaging.

## Projects

- `LayerZero.Fulfillment.Contracts`: HTTP contracts, commands, events, DTOs, and scenarios
- `LayerZero.Fulfillment.Api`: Minimal API ingress plus operator endpoints
- `LayerZero.Fulfillment.Processing`: command handlers and workflow orchestration
- `LayerZero.Fulfillment.Projections`: read-model, analytics, audit, and notification handlers
- `LayerZero.Fulfillment.Bootstrap`: explicit topology provisioning for local and test runs
- `LayerZero.Fulfillment.Client`: explicit typed client for the API surface
- broker-specific AppHosts that each run one fulfillment stack

## Prerequisites

- .NET 10 SDK
- Docker Desktop, Podman, or another OCI-compatible runtime
- enough free local ports for the selected broker AppHost and its API endpoints

The API launch profiles are fixed:

- HTTP: `http://localhost:5380`
- HTTPS: `https://localhost:7380`

## Local Runs

Run a broker-specific AppHost:

```bash
dotnet run --project samples/LayerZero.Fulfillment.RabbitMq.AppHost
dotnet run --project samples/LayerZero.Fulfillment.AzureServiceBus.AppHost
dotnet run --project samples/LayerZero.Fulfillment.Kafka.AppHost
dotnet run --project samples/LayerZero.Fulfillment.Nats.AppHost
```

For VS Code, use the checked-in broker-specific Aspire configurations with the
Microsoft Aspire extension and C# Dev Kit. That is the supported distributed-app
debug path for the sample:

- `Aspire: Fulfillment RabbitMQ AppHost`
- `Aspire: Fulfillment Azure Service Bus AppHost`
- `Aspire: Fulfillment Kafka AppHost`
- `Aspire: Fulfillment NATS AppHost`

Each broker AppHost dashboard uses stable development URLs:

- RabbitMQ: `https://localhost:17134` and `http://localhost:15170`
- Azure Service Bus emulator: `https://localhost:17135` and `http://localhost:15171`
- Kafka: `https://localhost:17136` and `http://localhost:15172`
- NATS JetStream: `https://localhost:17137` and `http://localhost:15173`

Choose the AppHost that matches the broker you want to exercise:

- RabbitMQ
- Azure Service Bus emulator
- Kafka
- NATS JetStream

Each broker AppHost exposes its API on stable AppHost-managed URLs:

- RabbitMQ: `http://localhost:5381` and `https://localhost:7381`
- Azure Service Bus emulator: `http://localhost:5382` and `https://localhost:7382`
- Kafka: `http://localhost:5383` and `https://localhost:7383`
- NATS JetStream: `http://localhost:5384` and `https://localhost:7384`

Those are the stable AppHost proxy URLs. Under Aspire's default proxy model the
API process itself usually binds a different allocated loopback port, so
resource logs can legitimately show a different internal port.

Each AppHost provisions its own topology through `LayerZero.Fulfillment.Bootstrap`
before the API, processing worker, and projections worker start. Each AppHost
also shares one SQLite database file across bootstrap, API, processing, and
projections:

- `samples/LayerZero.Fulfillment.RabbitMq.AppHost/data/fulfillment.db`
- `samples/LayerZero.Fulfillment.AzureServiceBus.AppHost/data/fulfillment.db`
- `samples/LayerZero.Fulfillment.Kafka.AppHost/data/fulfillment.db`
- `samples/LayerZero.Fulfillment.Nats.AppHost/data/fulfillment.db`

The dashboard adds direct OpenAPI deep-links for the HTTP and HTTPS API
endpoints so `/openapi/v1.json` is one click away from the resource card.

The broker end-to-end tests validate the workflows and transports, and the
broker-specific AppHost startup tests validate Aspire orchestration. Treat the
checked-in AppHost launch profiles as the supported `dotnet run` startup
contract, and treat the checked-in Aspire VS Code debug configurations as the
supported editor launch path.

If the AppHost HTTPS profile fails because the local development certificate is
missing, trust the standard .NET dev certificate:

```bash
dotnet dev-certs https --trust
```

Launching the AppHost without a launch profile is not supported. Aspire AppHost
startup requires the dashboard and resource-service settings supplied by the
checked-in launch profile or by an equivalent debugger configuration.

When switching brokers, start from a clean local session. Stale standalone
`LayerZero.Fulfillment.*` processes can make public proxy URLs, internal ports,
and resource logs disagree in confusing ways.

In VS Code, the editor Run or Debug button on an arbitrary `.cs` file launches
that file's associated project, not the AppHost. Use
the matching `Aspire: Fulfillment ... AppHost` configuration when you want to
debug the fulfillment orchestration host.

The supported VS Code debugger type for the AppHost is `aspire`, not `dotnet`.
Debugger-only user-unhandled exception stops are not the same thing as real
resource failures. Use the Aspire dashboard resource state and resource logs to
judge whether a fulfillment resource actually failed.

Run a single host manually:

```bash
dotnet run --project samples/LayerZero.Fulfillment.Api
dotnet run --project samples/LayerZero.Fulfillment.Bootstrap
dotnet run --project samples/LayerZero.Fulfillment.Processing
dotnet run --project samples/LayerZero.Fulfillment.Projections
```

## API Surface

Operator and workflow endpoints:

- `POST /orders`
- `POST /orders/{id}/cancel`
- `GET /orders/{id}`
- `GET /orders/{id}/timeline`
- `GET /deadletters`
- `POST /deadletters/{messageId}?handlerIdentity=...`

Example order creation body:

```json
{
  "customerEmail": "alex@example.com",
  "items": [
    { "sku": "keyboard-01", "quantity": 1 },
    { "sku": "mouse-01", "quantity": 2 }
  ],
  "shippingAddress": {
    "recipient": "Alex Example",
    "line1": "1 Main Street",
    "city": "Riga",
    "countryCode": "LV",
    "postalCode": "LV-1001"
  },
  "scenario": {
    "forceInventoryFailure": false,
    "forcePaymentTimeoutOnce": false,
    "forcePaymentDecline": false,
    "forceDuplicateShipment": false,
    "forceProjectionPoisonMessage": false
  }
}
```

## Deterministic Scenarios

The sample keeps failure and retry behavior explicit through `OrderScenario`:

- `ForceInventoryFailure`: turns inventory allocation into a business rejection
- `ForcePaymentTimeoutOnce`: throws once, retries, then succeeds
- `ForcePaymentDecline`: returns a terminal business failure
- `ForceDuplicateShipment`: intentionally dispatches the shipment-preparation command twice to prove side-effect deduplication
- `ForceProjectionPoisonMessage`: makes a projection handler fail terminally so dead-lettering and replay stay visible

## Data And Operator Visibility

The sample uses raw `Microsoft.Data.Sqlite`, not EF Core.

SQLite stores:

- order state
- projection/read-model state
- idempotency checkpoints
- dead-letter records
- operator-visible order timeline entries

Timeline entries and dead-letter records include:

- message id
- handler identity
- attempt count
- broker name
- entity name
- correlation id
- trace parent

Dead-letter replay stays explicit. Requeueing can target a specific failed
handler by passing `handlerIdentity` on the requeue endpoint.

## Ordering And Affinity

All order-scoped messages use `OrderId` as the affinity key.

What that means by broker:

- Kafka: the affinity key becomes the record key, so related messages can stay on the same partition
- Azure Service Bus: the affinity key becomes the session id
- RabbitMQ and NATS: the affinity key is preserved as metadata, but correctness still relies on idempotent handlers and order-state guards

The sample is intentionally written so correctness does not depend on identical
FIFO guarantees across brokers.

## Retry And Dead-Letter Topology

Deterministic names come from LayerZero conventions:

- command entities: `lz.cmd.<message>`
- event entities: `lz.evt.<message>`
- consumer or subscription paths: `lz.sub.<application>.<handler>`
- dead-letter entities: `<entity>.deadletter`
- retry entities: `<entity>.retry.default`

The exact transport mapping differs by broker:

- RabbitMQ: direct command exchanges, fanout event exchanges, TTL retry queues, and DLX routing
- Azure Service Bus: queues for commands, topics and subscriptions for events, native DLQ
- Kafka: primary topic, retry topic, and dead-letter topic per logical message
- NATS JetStream: primary stream plus dead-letter stream with explicit durable consumers

## Transport-Disable Path

`LayerZero.Fulfillment.Api` supports `Messaging:DisableTransport=true`, but
only for OpenAPI generation and isolated HTTP or client tests. It is not the
normal runtime mode for the sample.

## Troubleshooting

- If API startup fails with topology validation errors, run the bootstrap host first or use the AppHost profile that does it for you.
- If the AppHost API resource card shows `localhost:538x` or `localhost:738x` but the API logs mention a different port, that is the expected Aspire proxy model rather than a misconfiguration by itself.
- If local tests fail before a broker starts, confirm the Docker daemon is running.
- If you notice two RabbitMQ containers during a full local test run, that is usually expected because transport integration tests and fulfillment end-to-end tests keep their broker fixtures isolated per project.
- If broker containers or `testcontainers-ryuk` sidecars remain after the run completes, that is stale-session leakage rather than normal behavior. List and remove stale repo-owned sessions with:

```bash
dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --list
dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --apply --older-than 30m
```

If those lingering containers come from a run before repo-owned labels were in
place, use the explicit session-id path once:

```bash
dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --apply --older-than 0m --session-id <testcontainers-session-id>
```

- If Azure Service Bus provisioning fails against the emulator, make sure the admin connection string points at the emulator's management endpoint.
- If timelines stop at intermediate states after cancel or duplicate scenarios, inspect the dead-letter list and worker logs first; the sample preserves terminal order states instead of overwriting them later.
- If duplicate shipment or payment side effects reappear, inspect the SQLite `side_effects` table and the idempotency store wiring before increasing broker retries.
