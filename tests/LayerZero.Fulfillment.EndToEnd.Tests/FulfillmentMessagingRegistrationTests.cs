using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging.AzureServiceBus.Configuration;
using LayerZero.Messaging.Kafka.Configuration;
using LayerZero.Messaging.Nats.Configuration;
using LayerZero.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

public sealed class FulfillmentMessagingRegistrationTests
{
    [Fact]
    public void Rabbitmq_prefers_connection_string_configuration_over_sample_defaults()
    {
        var options = BuildOptions<RabbitMqBusOptions>(
            "RabbitMq",
            "ConnectionStrings:rabbitmq",
            "amqp://guest:guest@127.0.0.1:5673/",
            "Messaging:RabbitMq:ConnectionString",
            "amqp://guest:guest@localhost:5672/");

        Assert.Equal("amqp://guest:guest@127.0.0.1:5673/", options.ConnectionString);
    }

    [Fact]
    public void Azure_service_bus_prefers_connection_string_configuration_over_sample_defaults()
    {
        var options = BuildOptions<AzureServiceBusBusOptions>(
            "AzureServiceBus",
            "ConnectionStrings:servicebus",
            "Endpoint=sb://localhost:60808/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            "Messaging:AzureServiceBus:ConnectionString",
            "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=local");

        Assert.Equal(
            "Endpoint=sb://localhost:60808/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            options.ConnectionString);
    }

    [Fact]
    public void Kafka_prefers_connection_string_configuration_over_sample_defaults()
    {
        var options = BuildOptions<KafkaBusOptions>(
            "Kafka",
            "ConnectionStrings:kafka",
            "127.0.0.1:19092",
            "Messaging:Kafka:BootstrapServers",
            "localhost:9092");

        Assert.Equal("127.0.0.1:19092", options.BootstrapServers);
    }

    [Fact]
    public void Nats_prefers_connection_string_configuration_over_sample_defaults()
    {
        var options = BuildOptions<NatsBusOptions>(
            "Nats",
            "ConnectionStrings:nats",
            "nats://127.0.0.1:14222",
            "Messaging:Nats:Url",
            "nats://localhost:4222");

        Assert.Equal("nats://127.0.0.1:14222", options.Url);
    }

    private static TOptions BuildOptions<TOptions>(
        string broker,
        string connectionStringKey,
        string connectionStringValue,
        string sampleDefaultKey,
        string sampleDefaultValue)
        where TOptions : class
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:Broker"] = broker,
                [connectionStringKey] = connectionStringValue,
                [sampleDefaultKey] = sampleDefaultValue,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddFulfillmentMessaging(configuration, applicationName: "fulfillment-tests");

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<TOptions>>().Get("primary");
    }
}
