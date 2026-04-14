using LayerZero.Messaging.IntegrationTesting;
using Testcontainers.Kafka;
using Testcontainers.Nats;
using Testcontainers.RabbitMq;
using Testcontainers.ServiceBus;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

public interface IFulfillmentBrokerFixture
{
    string BrokerName { get; }

    void ApplyConfiguration(IDictionary<string, string?> settings);
}

public sealed class RabbitMqFulfillmentFixture : TestcontainerFixtureBase<RabbitMqContainer>, IFulfillmentBrokerFixture
{
    public string BrokerName => "RabbitMq";

    public RabbitMqFulfillmentFixture()
        : base("LayerZero.Fulfillment.EndToEnd.Tests", "rabbitmq")
    {
    }

    public void ApplyConfiguration(IDictionary<string, string?> settings)
    {
        settings["Messaging:Broker"] = BrokerName;
        settings["Messaging:RabbitMq:ConnectionString"] = Container.GetConnectionString();
        settings["Messaging:RabbitMq:RetryDelay"] = "00:00:00.200";
        settings["Messaging:RabbitMq:MaxDeliveryAttempts"] = "2";
        settings["Messaging:RabbitMq:PrefetchCount"] = "8";
    }

    protected override ValueTask<RabbitMqContainer> CreateContainerAsync(TestcontainerFixtureMetadata metadata)
    {
        return ValueTask.FromResult(
            ApplyContainerDefaults(new RabbitMqBuilder("rabbitmq:3.11"))
                .Build());
    }
}

public sealed class KafkaFulfillmentFixture : TestcontainerFixtureBase<KafkaContainer>, IFulfillmentBrokerFixture
{
    public string BrokerName => "Kafka";

    public KafkaFulfillmentFixture()
        : base("LayerZero.Fulfillment.EndToEnd.Tests", "kafka")
    {
    }

    public void ApplyConfiguration(IDictionary<string, string?> settings)
    {
        settings["Messaging:Broker"] = BrokerName;
        settings["Messaging:Kafka:BootstrapServers"] = Container.GetBootstrapAddress();
        settings["Messaging:Kafka:PollInterval"] = "00:00:00.100";
        settings["Messaging:Kafka:PartitionCount"] = "3";
        settings["Messaging:Kafka:MaxDeliveryAttempts"] = "2";
    }

    protected override ValueTask<KafkaContainer> CreateContainerAsync(TestcontainerFixtureMetadata metadata)
    {
        return ValueTask.FromResult(
            ApplyContainerDefaults(new KafkaBuilder("confluentinc/cp-kafka:7.5.12"))
                .WithKRaft()
                .Build());
    }
}

public sealed class NatsFulfillmentFixture : TestcontainerFixtureBase<NatsContainer>, IFulfillmentBrokerFixture
{
    public string BrokerName => "Nats";

    public NatsFulfillmentFixture()
        : base("LayerZero.Fulfillment.EndToEnd.Tests", "nats")
    {
    }

    public void ApplyConfiguration(IDictionary<string, string?> settings)
    {
        settings["Messaging:Broker"] = BrokerName;
        settings["Messaging:Nats:Url"] = Container.GetConnectionString();
        settings["Messaging:Nats:RetryDelay"] = "00:00:00.200";
        settings["Messaging:Nats:MaxDeliver"] = "2";
    }

    protected override ValueTask<NatsContainer> CreateContainerAsync(TestcontainerFixtureMetadata metadata)
    {
        return ValueTask.FromResult(
            ApplyContainerDefaults(new NatsBuilder("nats:2.9"))
                .WithCommand("-js")
                .Build());
    }
}

public sealed class AzureServiceBusFulfillmentFixture : TestcontainerFixtureBase<ServiceBusContainer>, IFulfillmentBrokerFixture
{
    public string BrokerName => "AzureServiceBus";

    public AzureServiceBusFulfillmentFixture()
        : base("LayerZero.Fulfillment.EndToEnd.Tests", "servicebus")
    {
    }

    public void ApplyConfiguration(IDictionary<string, string?> settings)
    {
        settings["Messaging:Broker"] = BrokerName;
        settings["Messaging:AzureServiceBus:ConnectionString"] = Container.GetConnectionString();
        settings["Messaging:AzureServiceBus:AdministrationConnectionString"] = Container.GetHttpConnectionString();
        settings["Messaging:AzureServiceBus:PrefetchCount"] = "8";
        settings["Messaging:AzureServiceBus:MaxConcurrentCalls"] = "2";
        settings["Messaging:AzureServiceBus:MaxAutoLockRenewalDuration"] = "00:00:30";
        settings["Messaging:AzureServiceBus:MaxDeliveryCount"] = "2";
    }

    protected override ValueTask<ServiceBusContainer> CreateContainerAsync(TestcontainerFixtureMetadata metadata)
    {
        return ValueTask.FromResult(
            ApplyContainerDefaults(new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest"))
                .WithAcceptLicenseAgreement(true)
                .Build());
    }
}

public sealed class CloudAzureServiceBusFulfillmentFixture : IFulfillmentBrokerFixture, IAsyncLifetime
{
    public string BrokerName => "AzureServiceBus";

    private string connectionString = string.Empty;
    private string administrationConnectionString = string.Empty;

    public void ApplyConfiguration(IDictionary<string, string?> settings)
    {
        settings["Messaging:Broker"] = BrokerName;
        settings["Messaging:AzureServiceBus:ConnectionString"] = connectionString;
        settings["Messaging:AzureServiceBus:AdministrationConnectionString"] = administrationConnectionString;
        settings["Messaging:AzureServiceBus:PrefetchCount"] = "8";
        settings["Messaging:AzureServiceBus:MaxConcurrentCalls"] = "2";
        settings["Messaging:AzureServiceBus:MaxAutoLockRenewalDuration"] = "00:00:30";
        settings["Messaging:AzureServiceBus:MaxDeliveryCount"] = "2";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public ValueTask InitializeAsync()
    {
        connectionString = Environment.GetEnvironmentVariable("LAYERZERO_AZURE_SERVICE_BUS_CLOUD_CONNECTION_STRING") ?? string.Empty;
        administrationConnectionString = Environment.GetEnvironmentVariable("LAYERZERO_AZURE_SERVICE_BUS_CLOUD_ADMIN_CONNECTION_STRING")
            ?? connectionString;
        return ValueTask.CompletedTask;
    }
}
