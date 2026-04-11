# NATS Adapter

Package: `LayerZero.Messaging.Nats`

## Prerequisites

- NATS server with JetStream enabled
- NATS URL, for example: `nats://localhost:4222`
- stream and durable consumer provisioning completed before production startup,
  or explicit local provisioning through the bootstrap project

## Configuration

Use `Messaging:Broker=Nats` and configure:

- `Messaging:Nats:Url`
- `Messaging:Nats:RetryDelay`
- `Messaging:Nats:MaxDeliver`
- `Messaging:Nats:EnableConsumers`

Aspire-hosted local runs can also provide the broker reference through
`ConnectionStrings:nats`.

## Defaults And Quirks

- JetStream is the default and only durable mode in this adapter
- consumers use explicit ack/Nak behavior
- retries use delayed redelivery backed by `AckWait` and JetStream backoff
- terminal failures publish to dead-letter subjects/streams
- `MaxDeliver` is the bounded retry budget before dead-lettering
- affinity is carried as subject/header metadata, not a universal FIFO promise

## Operational Notes

- JetStream must be enabled on the server or validation will fail immediately
- `MaxDeliver` should match the desired retry budget before dead-lettering
- stream names and durable consumer names are deterministic, so deleting them
  manually will break validate-only startup until bootstrap reprovisions them
- if dead-letter traffic grows, inspect the handler idempotency and failure
  classification before increasing retry counts
