using DotNet.Testcontainers.Builders;
using Testcontainers.Kafka;
using Testcontainers.Nats;
using Testcontainers.RabbitMq;
using Testcontainers.ServiceBus;
using Xunit.Sdk;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

public interface IFulfillmentBrokerFixture
{
    string BrokerName { get; }

    void ApplyConfiguration(IDictionary<string, string?> settings);
}

public sealed class RabbitMqFulfillmentFixture : IFulfillmentBrokerFixture, IAsyncLifetime, IAsyncDisposable
{
    public string BrokerName => "RabbitMq";

    private RabbitMqContainer container = null!;

    public void ApplyConfiguration(IDictionary<string, string?> settings)
    {
        settings["Messaging:Broker"] = BrokerName;
        settings["Messaging:RabbitMq:ConnectionString"] = container.GetConnectionString();
        settings["Messaging:RabbitMq:RetryDelay"] = "00:00:00.200";
        settings["Messaging:RabbitMq:MaxDeliveryAttempts"] = "2";
        settings["Messaging:RabbitMq:PrefetchCount"] = "8";
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask InitializeAsync()
    {
        container = new RabbitMqBuilder("rabbitmq:3.11").Build();
        await container.StartAsync().ConfigureAwait(false);
    }
}

public sealed class KafkaFulfillmentFixture : IFulfillmentBrokerFixture, IAsyncLifetime, IAsyncDisposable
{
    public string BrokerName => "Kafka";

    private KafkaContainer container = null!;

    public void ApplyConfiguration(IDictionary<string, string?> settings)
    {
        settings["Messaging:Broker"] = BrokerName;
        settings["Messaging:Kafka:BootstrapServers"] = container.GetBootstrapAddress();
        settings["Messaging:Kafka:PollInterval"] = "00:00:00.100";
        settings["Messaging:Kafka:PartitionCount"] = "3";
        settings["Messaging:Kafka:MaxDeliveryAttempts"] = "2";
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask InitializeAsync()
    {
        container = new KafkaBuilder("confluentinc/cp-kafka:7.5.12")
            .WithKRaft()
            .Build();
        await container.StartAsync().ConfigureAwait(false);
    }
}

public sealed class NatsFulfillmentFixture : IFulfillmentBrokerFixture, IAsyncLifetime, IAsyncDisposable
{
    public string BrokerName => "Nats";

    private NatsContainer container = null!;

    public void ApplyConfiguration(IDictionary<string, string?> settings)
    {
        settings["Messaging:Broker"] = BrokerName;
        settings["Messaging:Nats:Url"] = container.GetConnectionString();
        settings["Messaging:Nats:RetryDelay"] = "00:00:00.200";
        settings["Messaging:Nats:MaxDeliver"] = "2";
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask InitializeAsync()
    {
        container = new NatsBuilder("nats:2.9")
            .WithCommand("-js")
            .Build();
        await container.StartAsync().ConfigureAwait(false);
    }
}

public sealed class AzureServiceBusFulfillmentFixture : IFulfillmentBrokerFixture, IAsyncLifetime, IAsyncDisposable
{
    public string BrokerName => "AzureServiceBus";

    private ServiceBusContainer container = null!;

    public void ApplyConfiguration(IDictionary<string, string?> settings)
    {
        settings["Messaging:Broker"] = BrokerName;
        settings["Messaging:AzureServiceBus:ConnectionString"] = container.GetConnectionString();
        settings["Messaging:AzureServiceBus:AdministrationConnectionString"] = container.GetHttpConnectionString();
        settings["Messaging:AzureServiceBus:PrefetchCount"] = "8";
        settings["Messaging:AzureServiceBus:MaxConcurrentCalls"] = "2";
        settings["Messaging:AzureServiceBus:MaxAutoLockRenewalDuration"] = "00:00:30";
        settings["Messaging:AzureServiceBus:MaxDeliveryCount"] = "2";
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask InitializeAsync()
    {
        container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
            .WithAcceptLicenseAgreement(true)
            .Build();
        await container.StartAsync().ConfigureAwait(false);
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
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw SkipException.ForSkip("Set LAYERZERO_AZURE_SERVICE_BUS_CLOUD_CONNECTION_STRING to run Azure Service Bus cloud fulfillment parity tests.");
        }

        administrationConnectionString = Environment.GetEnvironmentVariable("LAYERZERO_AZURE_SERVICE_BUS_CLOUD_ADMIN_CONNECTION_STRING")
            ?? connectionString;
        return ValueTask.CompletedTask;
    }
}
