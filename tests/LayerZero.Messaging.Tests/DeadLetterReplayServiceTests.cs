using System.Text.Json.Serialization;
using LayerZero.Core;
using LayerZero.Messaging;
using LayerZero.Messaging.Operations;
using LayerZero.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Messaging.Tests;

public sealed partial class DeadLetterReplayServiceTests
{
    [Fact]
    public async Task RequeueAsync_returns_false_when_no_archived_record_exists()
    {
        var serializer = new MessageEnvelopeSerializer();
        var descriptor = CreateCommandDescriptor();
        var store = new FakeDeadLetterStore();
        using var provider = CreateProvider(store, serializer, descriptor, new FakeTransport());
        var replayService = provider.GetRequiredService<IDeadLetterReplayService>();

        var requeued = await replayService.RequeueAsync("missing", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(requeued);
        Assert.Equal(0, store.MarkRequeuedCalls);
    }

    [Fact]
    public async Task RequeueAsync_sends_commands_and_marks_the_record_requeued_after_success()
    {
        var serializer = new MessageEnvelopeSerializer();
        var descriptor = CreateCommandDescriptor();
        var transport = new FakeTransport();
        var store = new FakeDeadLetterStore
        {
            Envelope = new DeadLetterEnvelope(
                "primary",
                serializer.Serialize(
                    descriptor,
                    new TestCommand("settle-order"),
                    new MessageContext(
                        "msg-1",
                        descriptor.Name,
                        MessageKind.Command,
                        "primary",
                        "corr-1",
                        null,
                        null,
                        null,
                        DateTimeOffset.UtcNow,
                        4)).ToArray()),
        };

        using var provider = CreateProvider(store, serializer, descriptor, transport);
        var replayService = provider.GetRequiredService<IDeadLetterReplayService>();

        var requeued = await replayService.RequeueAsync("msg-1", "tests.command", TestContext.Current.CancellationToken);

        Assert.True(requeued);
        var sentMessage = Assert.Single(transport.SentMessages);
        Assert.Equal(0, sentMessage.Context.Attempt);
        Assert.Equal(1, store.MarkRequeuedCalls);
    }

    [Fact]
    public async Task RequeueAsync_does_not_mark_records_requeued_when_event_publish_fails()
    {
        var serializer = new MessageEnvelopeSerializer();
        var descriptor = CreateEventDescriptor();
        var transport = new FakeTransport { ThrowOnPublish = true };
        var store = new FakeDeadLetterStore
        {
            Envelope = new DeadLetterEnvelope(
                "primary",
                serializer.Serialize(
                    descriptor,
                    new TestEvent("shipment-prepared"),
                    new MessageContext(
                        "msg-2",
                        descriptor.Name,
                        MessageKind.Event,
                        "primary",
                        "corr-2",
                        null,
                        null,
                        null,
                        DateTimeOffset.UtcNow,
                        3)).ToArray()),
        };

        using var provider = CreateProvider(store, serializer, descriptor, transport);
        var replayService = provider.GetRequiredService<IDeadLetterReplayService>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => replayService.RequeueAsync("msg-2", "tests.event", TestContext.Current.CancellationToken));

        Assert.Equal(0, store.MarkRequeuedCalls);
    }

    private static ServiceProvider CreateProvider(
        FakeDeadLetterStore store,
        MessageEnvelopeSerializer serializer,
        MessageDescriptor descriptor,
        FakeTransport transport)
    {
        var services = new ServiceCollection();
        services.AddMessagingOperations();
        services.AddSingleton<IDeadLetterStore>(store);
        services.AddSingleton<IMessageRegistry>(new FakeRegistry(descriptor));
        services.AddSingleton<IMessageTransportResolver>(new FakeTransportResolver(transport));
        services.AddSingleton(serializer);
        return services.BuildServiceProvider();
    }

    private static MessageDescriptor CreateCommandDescriptor()
    {
        return new MessageDescriptor(
            MessageNames.For<TestCommand>(),
            typeof(TestCommand),
            MessageKind.Command,
            DeadLetterReplayJsonContext.Default.GetTypeInfo(typeof(TestCommand))!,
            MessageTopologyNames.Entity(MessageKind.Command, MessageNames.For<TestCommand>()));
    }

    private static MessageDescriptor CreateEventDescriptor()
    {
        return new MessageDescriptor(
            MessageNames.For<TestEvent>(),
            typeof(TestEvent),
            MessageKind.Event,
            DeadLetterReplayJsonContext.Default.GetTypeInfo(typeof(TestEvent))!,
            MessageTopologyNames.Entity(MessageKind.Event, MessageNames.For<TestEvent>()));
    }

    private sealed class FakeDeadLetterStore : IDeadLetterStore
    {
        public DeadLetterEnvelope? Envelope { get; set; }

        public int MarkRequeuedCalls { get; private set; }

        public Task<IReadOnlyList<DeadLetterEntry>> GetDeadLettersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeadLetterEntry>>([]);
        }

        public Task<DeadLetterEnvelope?> GetEnvelopeAsync(string messageId, string? handlerIdentity = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Envelope);
        }

        public Task MarkRequeuedAsync(string messageId, string? handlerIdentity = null, CancellationToken cancellationToken = default)
        {
            MarkRequeuedCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRegistry(MessageDescriptor descriptor) : IMessageRegistry
    {
        public IReadOnlyList<MessageDescriptor> Messages { get; } = [descriptor];

        public bool TryGetDescriptor(Type messageType, out MessageDescriptor resolved)
        {
            if (messageType == descriptor.MessageType)
            {
                resolved = descriptor;
                return true;
            }

            resolved = null!;
            return false;
        }

        public bool TryGetDescriptor(string messageName, out MessageDescriptor resolved)
        {
            if (string.Equals(messageName, descriptor.Name, StringComparison.Ordinal))
            {
                resolved = descriptor;
                return true;
            }

            resolved = null!;
            return false;
        }
    }

    private sealed class FakeTransportResolver(FakeTransport transport) : IMessageTransportResolver
    {
        public IMessageBusTransport Resolve(MessageDescriptor descriptor) => transport;

        public IMessageBusTransport Resolve(string busName) => transport;
    }

    private sealed class FakeTransport : IMessageBusTransport
    {
        public bool ThrowOnPublish { get; set; }

        public List<TransportMessage> SentMessages { get; } = [];

        public string Name => "primary";

        public ValueTask SendAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            if (ThrowOnPublish)
            {
                throw new InvalidOperationException("Publish failed.");
            }

            SentMessages.Add(message);
            return ValueTask.CompletedTask;
        }
    }

    private sealed record TestCommand(string Title) : ICommand;

    private sealed record TestEvent(string Title) : IEvent;

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(TestCommand))]
    [JsonSerializable(typeof(TestEvent))]
    private sealed partial class DeadLetterReplayJsonContext : JsonSerializerContext;
}
