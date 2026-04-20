using System.Text.Json.Serialization;
using LayerZero.Core;
using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.AzureServiceBus;
using LayerZero.Messaging.Kafka;
using LayerZero.Messaging.Nats;
using LayerZero.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayerZero.Messaging.Tests;

public sealed partial class MessagingTopologyTests
{
    [Fact]
    public void Topology_names_follow_the_deterministic_contract()
    {
        Assert.Equal("lz.cmd.orders.place", MessageTopologyNames.Entity(MessageKind.Command, "Orders.Place"));
        Assert.Equal("lz.evt.orders.completed", MessageTopologyNames.Entity(MessageKind.Event, "Orders Completed"));
        Assert.Equal("lz.sub.fulfillment.api.handler.process", MessageTopologyNames.Subscription("Fulfillment.Api", "Handler.Process"));
        Assert.Equal("lz.cmd.orders.place.deadletter", MessageTopologyNames.DeadLetter("lz.cmd.orders.place"));
        Assert.Equal("lz.cmd.orders.place.retry.fast", MessageTopologyNames.Retry("lz.cmd.orders.place", "Fast"));
    }

    [Fact]
    public async Task Conventions_resolve_affinity_from_attributes_and_explicit_overrides()
    {
        var descriptor = CreateDescriptor<AffinityCommand>(static command => command.OrderId.ToString("N"), affinityKeyMemberName: "OrderId");

        var services = new ServiceCollection();
        services.AddMessaging()
            .Affinity<AffinityCommand>(static command => $"tenant-{command.OrderId:N}");
        services.AddSingleton<IMessageRegistry>(new SingleMessageRegistry(descriptor));
        services.AddSingleton(new MessageBusRegistration("primary", typeof(FakeTransport)));
        services.AddKeyedSingleton<IMessageBusTransport>("primary", (_, _) => new FakeTransport("primary"));

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();
        var sender = provider.ServiceProvider.GetRequiredService<ICommandSender>();

        var command = new AffinityCommand(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var result = await sender.SendAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);

        var transport = (FakeTransport)provider.ServiceProvider.GetRequiredKeyedService<IMessageBusTransport>("primary");
        Assert.Single(transport.SentMessages);
        Assert.Equal("tenant-aaaaaaaabbbbccccddddeeeeeeeeeeee", transport.SentMessages[0].Context.AffinityKey);
    }

    [Fact]
    public void Route_resolver_prefers_convention_routes_over_the_single_bus_default()
    {
        var descriptor = CreateDescriptor<AffinityCommand>(static command => command.OrderId.ToString("N"), affinityKeyMemberName: "OrderId");

        var services = new ServiceCollection();
        services.AddMessaging()
            .Route<AffinityCommand>("secondary");
        services.AddSingleton<IMessageRegistry>(new SingleMessageRegistry(descriptor));
        services.AddSingleton(new MessageBusRegistration("primary", typeof(FakeTransport)));
        services.AddSingleton(new MessageBusRegistration("secondary", typeof(FakeTransport)));

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IMessageRouteResolver>();

        Assert.Equal("secondary", resolver.Resolve(descriptor));
    }

    [Fact]
    public async Task Rabbitmq_registration_adds_transport_topology_manager_and_consumer()
    {
        var services = CreateAdapterServices();
        services.AddMessaging().AddRabbitMqBus("primary", options => options.ConnectionString = "amqp://guest:guest@localhost:5672");

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();

        Assert.Contains(provider.ServiceProvider.GetServices<MessageBusRegistration>(), registration => registration.Name == "primary");
        Assert.NotNull(provider.ServiceProvider.GetRequiredKeyedService<IMessageBusTransport>("primary"));
        Assert.Contains(provider.ServiceProvider.GetServices<IMessageTopologyManager>(), static manager => manager.GetType().FullName?.Contains("RabbitMqTopologyManager", StringComparison.Ordinal) == true);
        Assert.Contains(provider.ServiceProvider.GetServices<IHostedService>(), static hostedService => hostedService.GetType().FullName?.Contains("RabbitMqConsumerHostedService", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Azure_service_bus_registration_adds_transport_topology_manager_and_consumer()
    {
        var services = CreateAdapterServices();
        services.AddMessaging().AddAzureServiceBusBus("primary", options => options.ConnectionString = "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=demo");

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();

        Assert.Contains(provider.ServiceProvider.GetServices<MessageBusRegistration>(), registration => registration.Name == "primary");
        Assert.NotNull(provider.ServiceProvider.GetRequiredKeyedService<IMessageBusTransport>("primary"));
        Assert.Contains(provider.ServiceProvider.GetServices<IMessageTopologyManager>(), static manager => manager.GetType().FullName?.Contains("AzureServiceBusTopologyManager", StringComparison.Ordinal) == true);
        Assert.Contains(provider.ServiceProvider.GetServices<IHostedService>(), static hostedService => hostedService.GetType().FullName?.Contains("AzureServiceBusConsumerHostedService", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Kafka_registration_adds_transport_topology_manager_and_consumer()
    {
        var services = CreateAdapterServices();
        services.AddMessaging().AddKafkaBus("primary", options => options.BootstrapServers = "localhost:9092");

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();

        Assert.Contains(provider.ServiceProvider.GetServices<MessageBusRegistration>(), registration => registration.Name == "primary");
        Assert.NotNull(provider.ServiceProvider.GetRequiredKeyedService<IMessageBusTransport>("primary"));
        Assert.Contains(provider.ServiceProvider.GetServices<IMessageTopologyManager>(), static manager => manager.GetType().FullName?.Contains("KafkaTopologyManager", StringComparison.Ordinal) == true);
        Assert.Contains(provider.ServiceProvider.GetServices<IHostedService>(), static hostedService => hostedService.GetType().FullName?.Contains("KafkaConsumerHostedService", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Nats_registration_adds_transport_topology_manager_and_consumer()
    {
        var services = CreateAdapterServices();
        services.AddMessaging().AddNatsBus("primary", options => options.Url = "nats://localhost:4222");

        await using var provider = services.BuildServiceProvider().CreateAsyncScope();

        Assert.Contains(provider.ServiceProvider.GetServices<MessageBusRegistration>(), registration => registration.Name == "primary");
        Assert.NotNull(provider.ServiceProvider.GetRequiredKeyedService<IMessageBusTransport>("primary"));
        Assert.Contains(provider.ServiceProvider.GetServices<IMessageTopologyManager>(), static manager => manager.GetType().FullName?.Contains("NatsTopologyManager", StringComparison.Ordinal) == true);
        Assert.Contains(provider.ServiceProvider.GetServices<IHostedService>(), static hostedService => hostedService.GetType().FullName?.Contains("NatsConsumerHostedService", StringComparison.Ordinal) == true);
    }

    [Fact]
    public Task Rabbitmq_transport_roles_register_the_expected_service_graph()
    {
        return AssertTransportRolesAsync(
            static (builder, role) => builder.AddRabbitMqBus("primary", options => options.ConnectionString = "amqp://guest:guest@localhost:5672", role),
            "RabbitMqConsumerHostedService");
    }

    [Fact]
    public Task Azure_service_bus_transport_roles_register_the_expected_service_graph()
    {
        return AssertTransportRolesAsync(
            static (builder, role) => builder.AddAzureServiceBusBus("primary", options => options.ConnectionString = "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=demo", role),
            "AzureServiceBusConsumerHostedService");
    }

    [Fact]
    public Task Kafka_transport_roles_register_the_expected_service_graph()
    {
        return AssertTransportRolesAsync(
            static (builder, role) => builder.AddKafkaBus("primary", options => options.BootstrapServers = "localhost:9092", role),
            "KafkaConsumerHostedService");
    }

    [Fact]
    public Task Nats_transport_roles_register_the_expected_service_graph()
    {
        return AssertTransportRolesAsync(
            static (builder, role) => builder.AddNatsBus("primary", options => options.Url = "nats://localhost:4222", role),
            "NatsConsumerHostedService");
    }

    private static ServiceCollection CreateAdapterServices()
    {
        var descriptor = CreateDescriptor<AffinityCommand>(static command => command.OrderId.ToString("N"), affinityKeyMemberName: "OrderId");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMessageRegistry>(new SingleMessageRegistry(descriptor));
        services.AddSingleton<IMessageTopologyManifest>(new EmptyTopologyManifest(descriptor));
        return services;
    }

    private static async Task AssertTransportRolesAsync(Action<MessagingBuilder, MessageTransportRole> addTransport, string consumerHostedServiceTypeName)
    {
        await AssertTransportRoleAsync(addTransport, consumerHostedServiceTypeName, MessageTransportRole.SendOnly, expectTransport: true, expectTopologyManager: false, expectConsumerHostedService: false);
        await AssertTransportRoleAsync(addTransport, consumerHostedServiceTypeName, MessageTransportRole.Consumers, expectTransport: true, expectTopologyManager: true, expectConsumerHostedService: true);
        await AssertTransportRoleAsync(addTransport, consumerHostedServiceTypeName, MessageTransportRole.Administration, expectTransport: false, expectTopologyManager: true, expectConsumerHostedService: false);
    }

    private static async Task AssertTransportRoleAsync(
        Action<MessagingBuilder, MessageTransportRole> addTransport,
        string consumerHostedServiceTypeName,
        MessageTransportRole role,
        bool expectTransport,
        bool expectTopologyManager,
        bool expectConsumerHostedService)
    {
        var services = CreateAdapterServices();
        addTransport(services.AddMessaging(), role);

        await using var provider = services.BuildServiceProvider();

        Assert.Contains(provider.GetServices<MessageBusRegistration>(), static registration => registration.Name == "primary");
        Assert.Equal(expectTransport, CanResolveTransport(provider));
        Assert.Equal(expectTopologyManager, provider.GetServices<IMessageTopologyManager>().Any());
        Assert.Equal(
            expectConsumerHostedService,
            provider.GetServices<IHostedService>().Any(hostedService => string.Equals(hostedService.GetType().Name, consumerHostedServiceTypeName, StringComparison.Ordinal)));
    }

    private static bool CanResolveTransport(IServiceProvider provider)
    {
        try
        {
            _ = provider.GetRequiredService<IMessageTransportResolver>().Resolve("primary");
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static MessageDescriptor CreateDescriptor<TMessage>(Func<TMessage, string?> affinityAccessor, string? affinityKeyMemberName = null)
        where TMessage : class
    {
        return new MessageDescriptor(
            MessageNames.For<TMessage>(),
            typeof(TMessage),
            MessageKind.Command,
            MessagingTopologyTestJsonContext.Default.GetTypeInfo(typeof(TMessage))!,
            MessageTopologyNames.Entity(MessageKind.Command, MessageNames.For<TMessage>()),
            requiresIdempotency: false,
            affinityKeyMemberName,
            message => affinityAccessor((TMessage)message));
    }

    [AffinityKey(nameof(OrderId))]
    private sealed record AffinityCommand(Guid OrderId) : ICommand;

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(AffinityCommand))]
    private sealed partial class MessagingTopologyTestJsonContext : JsonSerializerContext;

    private sealed class SingleMessageRegistry(MessageDescriptor descriptor) : IMessageRegistry
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

    private sealed class EmptyTopologyManifest(MessageDescriptor descriptor) : IMessageTopologyManifest
    {
        private readonly MessageTopologyDescriptor[] messages =
        [
            new(new MessageDescriptor(
                descriptor.Name,
                descriptor.MessageType,
                descriptor.Kind,
                descriptor.JsonTypeInfo,
                descriptor.EntityName,
                descriptor.RequiresIdempotency,
                descriptor.AffinityMemberName,
                descriptor.DefaultAffinityKeyAccessor))
        ];

        public IReadOnlyList<MessageTopologyDescriptor> Messages => messages;

        public bool TryGetDescriptor(Type messageType, out MessageTopologyDescriptor descriptor)
        {
            descriptor = messages.Single();
            return descriptor.Message.MessageType == messageType;
        }

        public bool TryGetDescriptor(string messageName, out MessageTopologyDescriptor descriptor)
        {
            descriptor = messages.Single();
            return string.Equals(descriptor.Message.Name, messageName, StringComparison.Ordinal);
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
}
