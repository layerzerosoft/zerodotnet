using LayerZero.Core;
using LayerZero.Fulfillment.Contracts.Orders;
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
using LayerZero.Fulfillment.Shared;
using LayerZero.Data;
using LayerZero.Data.Configuration;
using LayerZero.Data.Postgres;
using LayerZero.Messaging;
using LayerZero.Messaging.AzureServiceBus;
using LayerZero.Messaging.AzureServiceBus.Configuration;
using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.IntegrationTesting;
using LayerZero.Messaging.Kafka;
using LayerZero.Messaging.Kafka.Configuration;
using LayerZero.Messaging.Nats;
using LayerZero.Messaging.Nats.Configuration;
using LayerZero.Messaging.Operations;
using LayerZero.Messaging.Operations.Postgres;
using LayerZero.Messaging.RabbitMq;
using LayerZero.Messaging.RabbitMq.Configuration;
using LayerZero.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        var builder = CreateHostBuilder("RabbitMq", new Dictionary<string, string?>
        {
            ["ConnectionStrings:Fulfillment"] = "Host=localhost;Port=5432;Database=fulfillment;Username=postgres;Password=postgres",
            ["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672/",
        });

        ConfigureProcessingServices("RabbitMq", builder.Services, builder.Configuration);

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
        var processingBuilder = CreateHostBuilder(broker, CreateBootstrapSettings(broker));
        ConfigureProcessingServices(broker, processingBuilder.Services, processingBuilder.Configuration);

        using var processingHost = processingBuilder.Build();
        using var processingScope = processingHost.Services.CreateScope();

        Assert.NotNull(processingScope.ServiceProvider.GetService<ICommandHandler<PlaceOrder>>());
        Assert.NotNull(processingScope.ServiceProvider.GetService<IEventHandler<OrderPlaced>>());

        var projectionBuilder = CreateHostBuilder(broker, CreateBootstrapSettings(broker));
        ConfigureProjectionsServices(broker, projectionBuilder.Services, projectionBuilder.Configuration);

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
        var builder = CreateHostBuilder(broker, CreateBootstrapSettings(broker));
        ConfigureBootstrapServices(broker, builder.Services, builder.Configuration);

        using var host = builder.Build();

        Assert.NotNull(host.Services.GetService<IMigrationRuntime>());
        Assert.NotNull(host.Services.GetService<IMessageTopologyProvisioner>());
        Assert.NotNull(host.Services.GetService<IDeadLetterStore>());
        Assert.NotNull(host.Services.GetService<IDeadLetterReplayService>());
        Assert.NotNull(host.Services.GetService<IMessageIdempotencyStore>());
        Assert.Equal(
            GetApplicationName(broker),
            host.Services.GetRequiredService<IOptions<MessagingOptions>>().Value.ApplicationName);

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

    private static HostApplicationBuilder CreateHostBuilder(string broker, IReadOnlyDictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Environment.ApplicationName = GetApplicationName(broker);
        builder.Configuration.AddInMemoryCollection(settings);
        return builder;
    }

    private static void ConfigureBootstrapServices(string broker, IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
        ConfigureBootstrapData(broker, services.AddData<FulfillmentStore>().UsePostgres("Fulfillment"));
        services.AddMessagingOperations().UsePostgres("Fulfillment");
        ConfigureMessagingTransport(broker, CreateBootstrapMessagingBuilder(broker, services), configuration, MessageTransportRole.Administration);
    }

    private static void ConfigureProcessingServices(string broker, IServiceCollection services, IConfiguration configuration)
    {
        ConfigureWorkerServices(services);
        ConfigureMessagingTransport(broker, CreateProcessingMessagingBuilder(broker, services), configuration, MessageTransportRole.Consumers);
    }

    private static void ConfigureProjectionsServices(string broker, IServiceCollection services, IConfiguration configuration)
    {
        ConfigureWorkerServices(services);
        ConfigureMessagingTransport(broker, CreateProjectionsMessagingBuilder(broker, services), configuration, MessageTransportRole.Consumers);
    }

    private static void ConfigureWorkerServices(IServiceCollection services)
    {
        services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
        services.AddData<FulfillmentStore>().UsePostgres("Fulfillment");
        services.AddMessagingOperations().UsePostgres("Fulfillment");
        services.AddFulfillmentStore();
    }

    private static void ConfigureMessagingTransport(
        string broker,
        MessagingBuilder messaging,
        IConfiguration configuration,
        MessageTransportRole role)
    {
        switch (broker)
        {
            case "RabbitMq":
                messaging.AddRabbitMq(configuration, role: role);
                break;
            case "AzureServiceBus":
                messaging.AddAzureServiceBus(configuration, role: role);
                break;
            case "Kafka":
                messaging.AddKafka(configuration, role: role);
                break;
            case "Nats":
                messaging.AddNats(configuration, role: role);
                break;
            default:
                throw new InvalidOperationException($"Unsupported broker '{broker}'.");
        }
    }

    private static void ConfigureBootstrapData(string broker, DataBuilder data)
    {
        switch (broker)
        {
            case "AzureServiceBus":
                data.UseMigrations<AzureServiceBusFulfillmentBootstrapEntryPoint>(options => options.Executor = GetBootstrapExecutor(broker));
                break;
            case "RabbitMq":
                data.UseMigrations<RabbitMqFulfillmentBootstrapEntryPoint>(options => options.Executor = GetBootstrapExecutor(broker));
                break;
            case "Kafka":
                data.UseMigrations<KafkaFulfillmentBootstrapEntryPoint>(options => options.Executor = GetBootstrapExecutor(broker));
                break;
            case "Nats":
                data.UseMigrations<NatsFulfillmentBootstrapEntryPoint>(options => options.Executor = GetBootstrapExecutor(broker));
                break;
            default:
                throw new InvalidOperationException($"Unsupported broker '{broker}'.");
        }
    }

    private static MessagingBuilder CreateBootstrapMessagingBuilder(string broker, IServiceCollection services)
    {
        return broker switch
        {
            "AzureServiceBus" => services.AddMessaging<AzureServiceBusFulfillmentBootstrapEntryPoint>(),
            "RabbitMq" => services.AddMessaging<RabbitMqFulfillmentBootstrapEntryPoint>(),
            "Kafka" => services.AddMessaging<KafkaFulfillmentBootstrapEntryPoint>(),
            "Nats" => services.AddMessaging<NatsFulfillmentBootstrapEntryPoint>(),
            _ => throw new InvalidOperationException($"Unsupported broker '{broker}'."),
        };
    }

    private static MessagingBuilder CreateProcessingMessagingBuilder(string broker, IServiceCollection services)
    {
        return broker switch
        {
            "AzureServiceBus" => services.AddMessaging<AzureServiceBusFulfillmentProcessingEntryPoint>(),
            "RabbitMq" => services.AddMessaging<RabbitMqFulfillmentProcessingEntryPoint>(),
            "Kafka" => services.AddMessaging<KafkaFulfillmentProcessingEntryPoint>(),
            "Nats" => services.AddMessaging<NatsFulfillmentProcessingEntryPoint>(),
            _ => throw new InvalidOperationException($"Unsupported broker '{broker}'."),
        };
    }

    private static MessagingBuilder CreateProjectionsMessagingBuilder(string broker, IServiceCollection services)
    {
        return broker switch
        {
            "AzureServiceBus" => services.AddMessaging<AzureServiceBusFulfillmentProjectionsEntryPoint>(),
            "RabbitMq" => services.AddMessaging<RabbitMqFulfillmentProjectionsEntryPoint>(),
            "Kafka" => services.AddMessaging<KafkaFulfillmentProjectionsEntryPoint>(),
            "Nats" => services.AddMessaging<NatsFulfillmentProjectionsEntryPoint>(),
            _ => throw new InvalidOperationException($"Unsupported broker '{broker}'."),
        };
    }

    private static string GetApplicationName(string broker)
    {
        return broker switch
        {
            "RabbitMq" => "fulfillment-rabbitmq",
            "AzureServiceBus" => "fulfillment-azure-service-bus",
            "Kafka" => "fulfillment-kafka",
            "Nats" => "fulfillment-nats",
            _ => throw new InvalidOperationException($"Unsupported broker '{broker}'."),
        };
    }

    private static string GetBootstrapExecutor(string broker)
    {
        return broker switch
        {
            "RabbitMq" => "fulfillment-rabbitmq-bootstrap",
            "AzureServiceBus" => "fulfillment-azure-service-bus-bootstrap",
            "Kafka" => "fulfillment-kafka-bootstrap",
            "Nats" => "fulfillment-nats-bootstrap",
            _ => throw new InvalidOperationException($"Unsupported broker '{broker}'."),
        };
    }

    private static Dictionary<string, string?> CreateBootstrapSettings(string broker)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Fulfillment"] = "Host=localhost;Port=5432;Database=fulfillment;Username=postgres;Password=postgres",
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
