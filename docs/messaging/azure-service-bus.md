# Azure Service Bus Adapter

Package: `LayerZero.Messaging.AzureServiceBus`

## Prerequisites

- Azure Service Bus namespace or the Azure Service Bus emulator
- AMQP connection string with manage/listen/send rights for the intended path
- optional separate administration connection string when the emulator or test
  harness exposes provisioning over a different endpoint
- explicit provisioning before startup for production environments

## Configuration

Use `Messaging:Broker=AzureServiceBus` and configure:

- `Messaging:AzureServiceBus:ConnectionString`
- `Messaging:AzureServiceBus:AdministrationConnectionString`
- `Messaging:AzureServiceBus:PrefetchCount`
- `Messaging:AzureServiceBus:MaxConcurrentCalls`
- `Messaging:AzureServiceBus:MaxAutoLockRenewalDuration`
- `Messaging:AzureServiceBus:MaxDeliveryCount`
- `Messaging:AzureServiceBus:EnableConsumers`

Aspire-hosted local runs can also provide the broker reference through
`ConnectionStrings:servicebus`.

## Defaults And Quirks

- uses `Azure.Messaging.ServiceBus`
- commands map to queues
- events map to topics with one deterministic subscription per
  application-plus-handler identity
- processors use `PeekLock`
- `AutoCompleteMessages` is disabled
- affinity-aware messages use Service Bus sessions
- dead-letter routing uses the native DLQ
- retry exhaustion is bounded by `MaxDeliveryCount`
- validate-only startup remains the default

## Operational Notes

- session-backed messages need a stable affinity key
- use `AdministrationConnectionString` when the emulator exposes a separate
  management endpoint from the AMQP client endpoint
- emulator support is part of the PR matrix, but the cloud parity workflow is
  still the place to validate session-heavy production behavior
- lock-loss symptoms usually point to handlers that exceed the configured lock
  renewal window
- if startup validation fails, confirm queues, topics, and subscriptions were
  provisioned with the correct `RequiresSession` setting for affinity-aware
  messages
