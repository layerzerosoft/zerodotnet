using LayerZero.Core;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.Shared;
using LayerZero.Fulfillment.AzureServiceBus.Bootstrap;
using LayerZero.Fulfillment.AzureServiceBus.Processing;
using LayerZero.Fulfillment.AzureServiceBus.Projections;
using LayerZero.Fulfillment.Kafka.Bootstrap;
using LayerZero.Fulfillment.Kafka.Processing;
using LayerZero.Fulfillment.Kafka.Projections;
using LayerZero.Fulfillment.Nats.Bootstrap;
using LayerZero.Fulfillment.Nats.Processing;
using LayerZero.Fulfillment.Nats.Projections;
using LayerZero.Fulfillment.RabbitMq.Bootstrap;
using LayerZero.Fulfillment.RabbitMq.Processing;
using LayerZero.Fulfillment.RabbitMq.Projections;
using LayerZero.Messaging;
using LayerZero.Messaging.AzureServiceBus;
using LayerZero.Messaging.AzureServiceBus.Configuration;
using LayerZero.Messaging.IntegrationTesting;
using LayerZero.Messaging.Kafka;
using LayerZero.Messaging.Kafka.Configuration;
using LayerZero.Messaging.Nats;
using LayerZero.Messaging.Nats.Configuration;
using LayerZero.Messaging.Operations;
using LayerZero.Messaging.RabbitMq;
using LayerZero.Messaging.RabbitMq.Configuration;
using LayerZero.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

[Trait("Category", "LocalFast")]
public sealed class FulfillmentMessagingRegistrationTests
{
    [Fact]
    public void Rabbitmq_prefers_connection_string_configuration_over_sample_defaults()
    {
        var options = BuildOptions<RabbitMqBusOptions>(
            "RabbitMq",
            ("ConnectionStrings:rabbitmq", "amqp://guest:guest@127.0.0.1:5673/"),
            ("Messaging:RabbitMq:ConnectionString", "amqp://guest:guest@localhost:5672/"));

        Assert.Equal("amqp://guest:guest@127.0.0.1:5673/", options.ConnectionString);
    }

    [Fact]
    public void Azure_service_bus_prefers_connection_string_configuration_over_sample_defaults()
    {
        var options = BuildOptions<AzureServiceBusBusOptions>(
            "AzureServiceBus",
            ("ConnectionStrings:servicebus", "Endpoint=sb://localhost:60808/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"),
            ("Messaging:AzureServiceBus:ConnectionString", "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=local"));

        Assert.Equal(
            "Endpoint=sb://localhost:60808/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            options.ConnectionString);
    }

    [Fact]
    public void Azure_service_bus_uses_explicit_administration_connection_string_when_present()
    {
        var options = BuildOptions<AzureServiceBusBusOptions>(
            "AzureServiceBus",
            ("ConnectionStrings:servicebus", "Endpoint=sb://localhost:60808/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"),
            ("Messaging:AzureServiceBus:ConnectionString", "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=local"),
            ("Messaging:AzureServiceBus:AdministrationConnectionString", "Endpoint=sb://localhost:5300/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"));

        Assert.Equal(
            "Endpoint=sb://localhost:60808/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            options.ConnectionString);
        Assert.Equal(
            "Endpoint=sb://localhost:5300/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            options.AdministrationConnectionString);
    }

    [Fact]
    public void Kafka_prefers_connection_string_configuration_over_sample_defaults()
    {
        var options = BuildOptions<KafkaBusOptions>(
            "Kafka",
            ("ConnectionStrings:kafka", "127.0.0.1:19092"),
            ("Messaging:Kafka:BootstrapServers", "localhost:9092"));

        Assert.Equal("127.0.0.1:19092", options.BootstrapServers);
    }

    [Fact]
    public void Nats_prefers_connection_string_configuration_over_sample_defaults()
    {
        var options = BuildOptions<NatsBusOptions>(
            "Nats",
            ("ConnectionStrings:nats", "nats://127.0.0.1:14222"),
            ("Messaging:Nats:Url", "nats://localhost:4222"));

        Assert.Equal("nats://127.0.0.1:14222", options.Url);
    }

    [Fact]
    public void Rabbitmq_worker_host_ignores_unrelated_generated_integration_testing_handlers()
    {
        _ = typeof(IntegrationTestHost);

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Fulfillment"] = "Host=localhost;Port=5432;Database=fulfillment;Username=postgres;Password=postgres",
            ["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672/",
        });

        RabbitMqFulfillmentProcessingHost.ConfigureServices(builder.Services, builder.Configuration);

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        Assert.Null(scope.ServiceProvider.GetService<ICommandHandler<HappyCommand>>());
        Assert.Null(scope.ServiceProvider.GetService<IEventHandler<HappyEvent>>());
    }

    [Theory]
    [InlineData("RabbitMq")]
    [InlineData("AzureServiceBus")]
    [InlineData("Kafka")]
    [InlineData("Nats")]
    public void Broker_specific_consumer_hosts_register_generated_handlers_from_their_role_libraries(string broker)
    {
        var processingBuilder = Host.CreateApplicationBuilder();
        processingBuilder.Configuration.AddInMemoryCollection(CreateBootstrapSettings(broker));

        switch (broker)
        {
            case "RabbitMq":
                RabbitMqFulfillmentProcessingHost.ConfigureServices(processingBuilder.Services, processingBuilder.Configuration);
                break;
            case "AzureServiceBus":
                AzureServiceBusFulfillmentProcessingHost.ConfigureServices(processingBuilder.Services, processingBuilder.Configuration);
                break;
            case "Kafka":
                KafkaFulfillmentProcessingHost.ConfigureServices(processingBuilder.Services, processingBuilder.Configuration);
                break;
            case "Nats":
                NatsFulfillmentProcessingHost.ConfigureServices(processingBuilder.Services, processingBuilder.Configuration);
                break;
            default:
                throw new InvalidOperationException($"Unsupported broker '{broker}'.");
        }

        using var processingHost = processingBuilder.Build();
        using var processingScope = processingHost.Services.CreateScope();

        Assert.NotNull(processingScope.ServiceProvider.GetService<ICommandHandler<PlaceOrder>>());
        Assert.NotNull(processingScope.ServiceProvider.GetService<IEventHandler<OrderPlaced>>());

        var projectionBuilder = Host.CreateApplicationBuilder();
        projectionBuilder.Configuration.AddInMemoryCollection(CreateBootstrapSettings(broker));

        switch (broker)
        {
            case "RabbitMq":
                RabbitMqFulfillmentProjectionsHost.ConfigureServices(projectionBuilder.Services, projectionBuilder.Configuration);
                break;
            case "AzureServiceBus":
                AzureServiceBusFulfillmentProjectionsHost.ConfigureServices(projectionBuilder.Services, projectionBuilder.Configuration);
                break;
            case "Kafka":
                KafkaFulfillmentProjectionsHost.ConfigureServices(projectionBuilder.Services, projectionBuilder.Configuration);
                break;
            case "Nats":
                NatsFulfillmentProjectionsHost.ConfigureServices(projectionBuilder.Services, projectionBuilder.Configuration);
                break;
            default:
                throw new InvalidOperationException($"Unsupported broker '{broker}'.");
        }

        using var projectionHost = projectionBuilder.Build();
        using var projectionScope = projectionHost.Services.CreateScope();

        Assert.NotEmpty(projectionScope.ServiceProvider.GetServices<IEventHandler<OrderPlaced>>());
        Assert.Contains(
            projectionScope.ServiceProvider.GetServices<IEventHandler<OrderPlaced>>(),
            static handler => string.Equals(
                handler.GetType().FullName,
                "LayerZero.Fulfillment.Projections.Handlers.OrderPlacedAuditProjection",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("RabbitMq")]
    [InlineData("AzureServiceBus")]
    [InlineData("Kafka")]
    [InlineData("Nats")]
    public void Broker_specific_bootstrap_hosts_register_migrations_and_topology_services_without_manual_overrides(string broker)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(CreateBootstrapSettings(broker));

        switch (broker)
        {
            case "RabbitMq":
                RabbitMqFulfillmentBootstrapHost.ConfigureServices(builder.Services, builder.Configuration);
                break;
            case "AzureServiceBus":
                AzureServiceBusFulfillmentBootstrapHost.ConfigureServices(builder.Services, builder.Configuration);
                break;
            case "Kafka":
                KafkaFulfillmentBootstrapHost.ConfigureServices(builder.Services, builder.Configuration);
                break;
            case "Nats":
                NatsFulfillmentBootstrapHost.ConfigureServices(builder.Services, builder.Configuration);
                break;
            default:
                throw new InvalidOperationException($"Unsupported broker '{broker}'.");
        }

        using var host = builder.Build();

        Assert.NotNull(host.Services.GetService<IMigrationRuntime>());
        Assert.NotNull(host.Services.GetService<IMessageTopologyProvisioner>());
        Assert.NotNull(host.Services.GetService<IDeadLetterStore>());
        Assert.NotNull(host.Services.GetService<IDeadLetterReplayService>());
        Assert.NotNull(host.Services.GetService<IMessageIdempotencyStore>());

        var topologyManifest = host.Services.GetRequiredService<IMessageTopologyManifest>();

        Assert.True(
            topologyManifest.TryGetDescriptor(typeof(PlaceOrder), out var placeOrderTopology),
            "The bootstrap host should include generated topology for the PlaceOrder command.");
        Assert.Contains(
            placeOrderTopology.Subscriptions,
            static subscription => string.Equals(
                subscription.HandlerType.FullName,
                "LayerZero.Fulfillment.Processing.Workflows.PlaceOrderHandler",
                StringComparison.Ordinal));

        Assert.True(
            topologyManifest.TryGetDescriptor(typeof(OrderPlaced), out var orderPlacedTopology),
            "The bootstrap host should include generated topology for the OrderPlaced event.");
        Assert.Contains(
            orderPlacedTopology.Subscriptions,
            static subscription => string.Equals(
                subscription.HandlerType.FullName,
                "LayerZero.Fulfillment.Projections.Handlers.OrderPlacedAuditProjection",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Fulfillment_shared_registration_stays_domain_only()
    {
        var services = new ServiceCollection();
        services.AddFulfillmentStore();

        using var provider = services.BuildServiceProvider();

        Assert.Contains(
            services,
            static descriptor => descriptor.ServiceType == typeof(FulfillmentStore)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Null(provider.GetService<IDeadLetterStore>());
        Assert.Null(provider.GetService<IDeadLetterReplayService>());
        Assert.Null(provider.GetService<IMessageIdempotencyStore>());
    }

    private static TOptions BuildOptions<TOptions>(
        string broker,
        params (string Key, string? Value)[] settings)
        where TOptions : class
    {
        var values = new Dictionary<string, string?>();

        foreach (var (key, value) in settings)
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        var messaging = services.AddMessaging("fulfillment-tests");
        switch (broker)
        {
            case "RabbitMq":
                messaging.AddRabbitMq(configuration, role: MessageTransportRole.SendOnly);
                break;
            case "AzureServiceBus":
                messaging.AddAzureServiceBus(configuration, role: MessageTransportRole.SendOnly);
                break;
            case "Kafka":
                messaging.AddKafka(configuration, role: MessageTransportRole.SendOnly);
                break;
            case "Nats":
                messaging.AddNats(configuration, role: MessageTransportRole.SendOnly);
                break;
            default:
                throw new InvalidOperationException($"Unsupported broker '{broker}'.");
        }

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<TOptions>>().Get("primary");
    }

    private static Dictionary<string, string?> CreateBootstrapSettings(string broker)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Fulfillment"] = "Host=localhost;Port=5432;Database=fulfillment;Username=postgres;Password=postgres",
            ["Messaging:ApplicationName"] = "fulfillment-bootstrap-tests",
        };

        switch (broker)
        {
            case "RabbitMq":
                settings["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672/";
                break;
            case "AzureServiceBus":
                settings["ConnectionStrings:servicebus"] = "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=local";
                settings["Messaging:AzureServiceBus:AdministrationConnectionString"] = "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=local";
                break;
            case "Kafka":
                settings["ConnectionStrings:kafka"] = "localhost:9092";
                break;
            case "Nats":
                settings["ConnectionStrings:nats"] = "nats://localhost:4222";
                break;
            default:
                throw new InvalidOperationException($"Unsupported broker '{broker}'.");
        }

        return settings;
    }
}
