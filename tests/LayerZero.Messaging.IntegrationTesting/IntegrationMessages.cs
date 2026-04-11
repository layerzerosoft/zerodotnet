using LayerZero.Core;

namespace LayerZero.Messaging.IntegrationTesting;

[AffinityKey(nameof(OrderId))]
public sealed record HappyCommand(Guid OrderId, string Payload) : ICommand;

[AffinityKey(nameof(OrderId))]
public sealed record HappyEvent(Guid OrderId, string Payload) : IEvent;

[AffinityKey(nameof(OrderId))]
public sealed record RetryCommand(Guid OrderId, string Payload) : ICommand;

[AffinityKey(nameof(OrderId))]
public sealed record PoisonCommand(Guid OrderId, string Payload) : ICommand;

[AffinityKey(nameof(OrderId))]
public sealed record PoisonEvent(Guid OrderId, string Payload) : IEvent;

[AffinityKey(nameof(OrderId))]
public sealed record RestartCommand(Guid OrderId, string Payload) : ICommand;

[AffinityKey(nameof(OrderId))]
public sealed record IdempotentCommand(Guid OrderId, string Payload) : ICommand;
