using System.Text.Json.Serialization;
using LayerZero.Core;
using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Tests;

public sealed partial class MessagingFoundationTests
{
    [Fact]
    public void Serializer_round_trips_envelope_metadata_and_payload()
    {
        var descriptor = CreateDescriptor(MessageKind.Command);
        var registry = new FakeRegistry(descriptor);
        var serializer = new Serialization.MessageEnvelopeSerializer();
        var timestamp = new DateTimeOffset(2026, 4, 10, 12, 30, 0, TimeSpan.Zero);
        var context = new MessageContext(
            "msg-1",
            descriptor.Name,
            MessageKind.Command,
            "primary",
            "corr-1",
            "cause-1",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00",
            "tenant=demo",
            timestamp,
            2,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tenant"] = "acme",
            });

        var body = serializer.Serialize(descriptor, new TestCommand("Draft docs"), context);
        var envelope = serializer.Deserialize(body, "primary", registry);

        Assert.Equal(descriptor.Name, envelope.Descriptor.Name);
        Assert.Equal("msg-1", envelope.Context.MessageId);
        Assert.Equal("corr-1", envelope.Context.CorrelationId);
        Assert.Equal("cause-1", envelope.Context.CausationId);
        Assert.Equal("tenant=demo", envelope.Context.TraceState);
        Assert.Equal(2, envelope.Context.Attempt);
        Assert.Equal("acme", envelope.Context.Headers["tenant"]);
        Assert.Equal("Draft docs", Assert.IsType<TestCommand>(envelope.Message).Title);
    }

    [Fact]
    public async Task Command_sender_uses_the_single_registered_bus_by_default()
    {
        var descriptor = CreateDescriptor(MessageKind.Command);
        var registry = new FakeRegistry(descriptor);
        var transport = new FakeTransport("primary");

        var services = new ServiceCollection();
        services.AddMessaging();
        services.AddSingleton<IMessageRegistry>(registry);
        services.AddSingleton(new MessageBusRegistration("primary", typeof(FakeTransport)));
        services.AddKeyedSingleton<IMessageBusTransport>("primary", (_, _) => transport);

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();
        var sender = provider.ServiceProvider.GetRequiredService<ICommandSender>();

        var result = await sender.SendAsync(new TestCommand("Queue me"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(transport.SentMessages);
        Assert.Equal(descriptor.Name, transport.SentMessages[0].Context.MessageName);
        Assert.Equal("primary", transport.SentMessages[0].Context.TransportName);
    }

    [Fact]
    public async Task Processor_deadletters_validation_failures_by_default()
    {
        var descriptor = CreateDescriptor(MessageKind.Command);
        var serializer = new LayerZero.Messaging.Serialization.MessageEnvelopeSerializer();
        var registry = new FakeRegistry(descriptor);
        var validation = LayerZero.Validation.ValidationResult.Invalid([
            new LayerZero.Validation.ValidationFailure("Title", "layerzero.validation.not_empty", "Title is required.")
        ]);

        var services = new ServiceCollection();
        services.AddMessaging(static options => options.ApplicationName = "tests");
        services.AddSingleton<IMessageRegistry>(registry);
        services.AddSingleton<IMessageHandlerInvoker>(new FakeInvoker(descriptor, MessageHandlingResult.ValidationFailure(validation), requiresIdempotency: false));

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();
        var processor = provider.ServiceProvider.GetRequiredService<IMessageProcessor>();
        var body = serializer.Serialize(
            descriptor,
            new TestCommand(string.Empty),
            new MessageContext("msg-2", descriptor.Name, MessageKind.Command, "primary", null, null, null, null, DateTimeOffset.UtcNow, 0));

        var result = await processor.ProcessAsync(
            body,
            "primary",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(MessageProcessingAction.DeadLetter, result.Action);
        Assert.Contains(result.Errors, error => error.Code == "layerzero.validation.not_empty");
    }

    [Fact]
    public async Task Startup_validation_requires_idempotency_store_when_any_handler_needs_it()
    {
        var descriptor = CreateDescriptor(MessageKind.Command);
        var services = new ServiceCollection();
        services.AddMessaging();
        services.AddSingleton<IMessageRegistry>(new FakeRegistry(descriptor));
        services.AddSingleton<IMessageHandlerInvoker>(new FakeInvoker(descriptor, MessageHandlingResult.Success(), requiresIdempotency: true));

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();
        var hostedService = Assert.Single(provider.ServiceProvider.GetServices<IHostedService>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => hostedService.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("IMessageIdempotencyStore", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_messaging_binds_application_name_from_configuration_when_available()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:ApplicationName"] = "configured-name",
            })
            .Build());
        services.AddMessaging();

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<MessagingOptions>>().Value;

        Assert.Equal("configured-name", options.ApplicationName);
    }

    [Fact]
    public void Add_messaging_prefers_explicit_application_name_over_configuration_and_host_defaults()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:ApplicationName"] = "configured-name",
            })
            .Build());
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("host-name"));
        services.AddMessaging("explicit-name");

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<MessagingOptions>>().Value;

        Assert.Equal("explicit-name", options.ApplicationName);
    }

    [Fact]
    public void Add_messaging_uses_host_application_name_when_configuration_is_missing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("host-name"));
        services.AddMessaging();

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<MessagingOptions>>().Value;

        Assert.Equal("host-name", options.ApplicationName);
    }

    [Fact]
    public void Add_messaging_without_configuration_or_host_environment_still_builds()
    {
        var services = new ServiceCollection();
        services.AddMessaging();

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<MessagingOptions>>().Value;

        Assert.Null(options.ApplicationName);
    }

    private static MessageDescriptor CreateDescriptor(MessageKind kind)
    {
        return new MessageDescriptor(
            MessageNames.For<TestCommand>(),
            typeof(TestCommand),
            kind,
            MessagingFoundationTestJsonContext.Default.GetTypeInfo(typeof(TestCommand))!,
            MessageTopologyNames.Entity(kind, MessageNames.For<TestCommand>()));
    }

    private sealed record TestCommand(string Title) : ICommand;

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(TestCommand))]
    private sealed partial class MessagingFoundationTestJsonContext : JsonSerializerContext;

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

    private sealed class FakeInvoker(
        MessageDescriptor descriptor,
        MessageHandlingResult result,
        bool requiresIdempotency) : IMessageHandlerInvoker
    {
        public MessageDescriptor Descriptor { get; } = descriptor;

        public string HandlerIdentity { get; } = "tests.fake";

        public bool RequiresIdempotency { get; } = requiresIdempotency;

        public ValueTask<MessageHandlingResult> InvokeAsync(
            IServiceProvider services,
            object message,
            MessageContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FakeTransport(string name) : IMessageBusTransport
    {
        public List<TransportMessage> SentMessages { get; } = [];

        public string Name { get; } = name;

        public ValueTask SendAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment(string applicationName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = applicationName;

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
