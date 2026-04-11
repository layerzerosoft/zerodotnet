# RabbitMQ Adapter

Package: `LayerZero.Messaging.RabbitMq`

## Prerequisites

- RabbitMQ 4.x or newer
- AMQP connection string, for example:
  `amqp://guest:guest@localhost:5672`
- topology provisioning completed before production startup, or explicit local
  provisioning through `IMessageTopologyManager.ProvisionAsync()`

## Configuration

Use `Messaging:Broker=RabbitMq` in the fulfillment sample and configure:

- `Messaging:RabbitMq:ConnectionString`
- `Messaging:RabbitMq:PrefetchCount`
- `Messaging:RabbitMq:RetryDelay`
- `Messaging:RabbitMq:MaxDeliveryAttempts`
- `Messaging:RabbitMq:EnableConsumers`
- `Messaging:RabbitMq:PublisherConfirmationTimeout`

Aspire-hosted local runs can also provide the broker reference through
`ConnectionStrings:rabbitmq`.

## Defaults And Quirks

- commands use durable direct exchanges and one durable queue per handler
- events use durable fanout exchanges and durable subscription queues
- consumers use manual ack
- retries move through explicit retry exchanges/queues with TTL + DLX
- dead-letter exchanges end with `.deadletter`
- retry exhaustion is bounded by `MaxDeliveryAttempts`
- affinity is metadata only; RabbitMQ does not receive a stronger ordering
  contract than the handler/state model can guarantee
- production startup validates topology by default instead of mutating it

## Operational Notes

- if validation fails on startup, confirm the exchanges and queues already
  exist or run the bootstrap project first
- queue names follow `lz.sub.<application>.<handler>` and retry/dead-letter
  queues derive from that base name
- if publisher confirms time out, check broker load and round-trip latency
- if consumers appear idle, verify `Messaging:RabbitMq:EnableConsumers=true`
