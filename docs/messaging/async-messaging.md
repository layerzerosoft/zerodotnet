# LayerZero Async Messaging

`LayerZero.Messaging` is the transport-neutral runtime for LayerZero command
and event flows. The repository now also ships first-party transport adapters:

- `LayerZero.Messaging.RabbitMq`
- `LayerZero.Messaging.AzureServiceBus`
- `LayerZero.Messaging.Kafka`
- `LayerZero.Messaging.Nats`

## Foundation Model

The common runtime surface is:

- `ICommandSender`
- `IEventPublisher`
- `MessageContext`
- `IMessageContextAccessor`
- `IMessageProcessor`
- `IMessageFailureClassifier`
- `IMessageIdempotencyStore`
- `IMessageTopologyManager`
- generated `AddMessages()`
- generated `IMessageRegistry`
- generated `IMessageTopologyManifest`

The source generator discovers validators, command handlers, event handlers,
message names, affinity metadata, and idempotency requirements at compile time.
There is no runtime assembly scanning in the default path.

Typical worker setup:

```csharp
builder.Services
    .AddMessaging(options => options.ApplicationName = "fulfillment-processing")
    .AddRabbitMqBus("primary", options =>
    {
        options.ConnectionString = "amqp://guest:guest@localhost:5672";
    })
    .Services
    .AddMessages();
```

## Processing Semantics

LayerZero keeps the transport-neutral contract small, but the behavior is
strict:

- commands are point-to-point and must resolve to exactly one durable consumer path
- events are pub/sub and may fan out to multiple durable subscriptions
- affinity is a locality hint carried by `MessageContext.AffinityKey`
- Kafka maps affinity to the record key, Azure Service Bus maps it to sessions,
  and RabbitMQ/NATS carry it as metadata while correctness still comes from
  idempotent handlers and state guards

## Envelope And Failure Defaults

Every adapter uses the same logical envelope fields:

- `messageId`
- `messageName`
- `messageKind`
- `transportName`
- `correlationId`
- `causationId`
- `traceParent`
- `traceState`
- `timestamp`
- `attempt`
- `affinityKey`
- arbitrary headers

Default failure policy:

- validation failures are terminal and dead-lettered
- `Result.Failure(...)` is terminal and dead-lettered
- thrown exceptions are retryable by default
- adapters use broker-native redelivery where possible, then move to dead-letter
- `IMessageFailureClassifier` can override the default

Idempotency is opt-in. If a message or handler requires idempotency, startup
validation fails unless an `IMessageIdempotencyStore` is registered.

## Topology Defaults

Deterministic names are shared across adapters:

- commands: `lz.cmd.<message>`
- events: `lz.evt.<message>`
- subscriptions/consumer groups: `lz.sub.<application>.<handler>`
- dead-letter entities: `<entity>.deadletter`
- retry entities: `<entity>.retry.<tier>`

Startup behavior:

- runtime defaults to topology validation on start
- provisioning is explicit through `IMessageTopologyManager.ProvisionAsync()`
- the fulfillment sample uses `LayerZero.Fulfillment.Bootstrap` to provision
  local topologies before worker hosts start
- production hosts should keep validate-only startup
- the API sample's `Messaging:DisableTransport=true` path is only for OpenAPI
  generation and isolated HTTP/client tests

## Broker Defaults

- RabbitMQ: direct exchanges for commands, fanout exchanges for events,
  long-lived connections/channels, manual ack, publisher confirms, retry queues
  with TTL and DLX, bounded delivery attempts, health checks, and passive
  validation by default.
- Azure Service Bus: `Azure.Messaging.ServiceBus`, `PeekLock`,
  `AutoCompleteMessages = false`, conservative prefetch, bounded concurrency,
  lock renewal, native dead-lettering, bounded delivery count, and
  session-aware affinity support.
- Kafka: `Confluent.Kafka`, idempotent producer, explicit offset store/commit
  after success, retry topics, dead-letter topics, bounded delivery attempts,
  and one consumer group per command/event subscription path.
- NATS: `NATS.Net` with JetStream only, durable consumers, explicit ack/Nak,
  retry delay/backoff, bounded `MaxDeliver`, and dead-letter subjects/streams.

## Verification

The repository now treats live-broker execution as part of the contract.

Included suites:

- `tests/LayerZero.Messaging.RabbitMq.IntegrationTests`
- `tests/LayerZero.Messaging.AzureServiceBus.IntegrationTests`
- `tests/LayerZero.Messaging.Kafka.IntegrationTests`
- `tests/LayerZero.Messaging.Nats.IntegrationTests`
- `tests/LayerZero.Fulfillment.EndToEnd.Tests`

Coverage includes:

- send and publish plus successful consumption
- trace and metadata propagation
- retryable exception then success
- terminal failure to dead-letter
- duplicate delivery with idempotent handlers
- topology validation versus explicit provisioning
- restart and redelivery behavior
- the fulfillment workflow matrix across every supported broker profile

Supported fulfillment E2E commands:

- fast local inner loop:
  `dotnet test tests/LayerZero.Fulfillment.EndToEnd.Tests/LayerZero.Fulfillment.EndToEnd.Tests.csproj --no-build --filter "Category=LocalFast"`
- full local fulfillment matrix:
  `dotnet test tests/LayerZero.Fulfillment.EndToEnd.Tests/LayerZero.Fulfillment.EndToEnd.Tests.csproj --no-build --filter "Category!=CloudOptional"`
- cloud Azure Service Bus parity:
  `dotnet test LayerZero.slnx --no-build --filter "Category=CloudOptional"`

Broker-specific setup notes live here:

- [RabbitMQ](rabbitmq.md)
- [Azure Service Bus](azure-service-bus.md)
- [Kafka](kafka.md)
- [NATS](nats.md)
- [Fulfillment sample runbook](fulfillment-sample.md)

## Fulfillment Sample

The flagship async sample is `samples/LayerZero.Fulfillment.*`.

Projects:

- `LayerZero.Fulfillment.Contracts`: HTTP contracts, commands, events, and DTOs
- `LayerZero.Fulfillment.Api`: order ingress API and operator endpoints
- `LayerZero.Fulfillment.Processing`: command handlers and orchestration
- `LayerZero.Fulfillment.Projections`: event fan-out, notifications, analytics, audit
- `LayerZero.Fulfillment.Bootstrap`: explicit topology provisioning
- `LayerZero.Fulfillment.Client`: explicit typed client
- broker-specific AppHosts for RabbitMQ, Azure Service Bus, Kafka, and NATS

Workflow focus:

- `PlaceOrder`
- `CancelOrder`
- `ReserveInventory`
- `AuthorizePayment`
- `PrepareShipment`
- `DispatchShipment`

The sample intentionally exercises:

- successful command enqueue and processing
- event fan-out
- transient retry then success
- terminal failure and dead-lettering
- duplicate-delivery protection on external side effects
- cancel during in-flight processing
- correlation and timeline visibility from HTTP ingress through worker handling

The sample uses raw `Microsoft.Data.Sqlite` for order state, dead letters,
idempotency checkpoints, and timelines.

## Local Prerequisites

- .NET 10 SDK
- Docker Desktop, Podman, or another OCI-compatible container runtime
- free local ports for RabbitMQ, Kafka, NATS, and the Azure Service Bus emulator

Local `dotnet test` runs require the same Docker daemon because the adapter
integration suites and fulfillment matrix use Testcontainers plus the official
Azure Service Bus emulator.

Testcontainers hygiene is part of the supported workflow:

- temporary duplicate broker containers during a full matrix run are expected
- lingering broker or `testcontainers-ryuk` containers after the run completes
  are not expected
- the supported local remediation path is:

```bash
dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --list
dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --apply --older-than 30m
```

For one-time cleanup of legacy unlabeled sessions that predate this hardening,
target them explicitly by Testcontainers session id:

```bash
dotnet run --project eng/LayerZero.Testcontainers.Cleanup -- --apply --older-than 0m --session-id <testcontainers-session-id>
```

Cloud parity for Azure Service Bus sessions is handled separately through the
`azure-service-bus-cloud-parity` workflow. Set:

- `LAYERZERO_AZURE_SERVICE_BUS_CLOUD_CONNECTION_STRING`
- `LAYERZERO_AZURE_SERVICE_BUS_CLOUD_ADMIN_CONNECTION_STRING`

For the broker-specific sample AppHosts:

```bash
dotnet run --project samples/LayerZero.Fulfillment.RabbitMq.AppHost
dotnet run --project samples/LayerZero.Fulfillment.AzureServiceBus.AppHost
dotnet run --project samples/LayerZero.Fulfillment.Kafka.AppHost
dotnet run --project samples/LayerZero.Fulfillment.Nats.AppHost
```

For the API alone:

```bash
dotnet run --project samples/LayerZero.Fulfillment.Api
```

The API launch profiles are:

- HTTP: `http://localhost:5380`
- HTTPS: `https://localhost:7380`

The typed client sample:

```bash
dotnet run --project samples/LayerZero.Fulfillment.Client -- https://localhost:7380
```

## Sample-Only Test Mode

`LayerZero.Fulfillment.Api` supports `Messaging:DisableTransport=true` for
OpenAPI document generation and HTTP/client integration tests. This is a
sample-only escape hatch so the API contract can be exercised without a live
broker. Normal runtime paths should keep transport validation enabled.
