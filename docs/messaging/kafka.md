# Kafka Adapter

Package: `LayerZero.Messaging.Kafka`

## Prerequisites

- Apache Kafka or a compatible Confluent platform deployment
- bootstrap servers string, for example: `localhost:9092`
- topic provisioning completed ahead of production startup, or explicit local
  provisioning through the bootstrap project

## Configuration

Use `Messaging:Broker=Kafka` and configure:

- `Messaging:Kafka:BootstrapServers`
- `Messaging:Kafka:PollInterval`
- `Messaging:Kafka:MaxDeliveryAttempts`
- `Messaging:Kafka:PartitionCount`
- `Messaging:Kafka:ReplicationFactor`
- `Messaging:Kafka:EnableConsumers`

Aspire-hosted local runs can also provide the broker reference through
`ConnectionStrings:kafka`.

## Defaults And Quirks

- one deterministic topic exists per logical command or event
- producer idempotence is enabled
- affinity keys map to Kafka message keys
- offsets are stored and committed only after successful processing
- retries use dedicated retry topics
- terminal failures use dead-letter topics
- retry exhaustion is bounded by `MaxDeliveryAttempts`
- consumer groups are deterministic per application + handler identity

## Operational Notes

- topic partitioning affects throughput and ordering guarantees
- command correctness is still idempotency-first even when affinity keys keep
  related messages on the same partition
- retry topics are part of the steady-state topology, not an emergency-only path
- if consumers restart from the wrong place, inspect committed offsets and the
  configured consumer group name
