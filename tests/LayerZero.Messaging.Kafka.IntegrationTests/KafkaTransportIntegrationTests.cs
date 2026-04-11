using LayerZero.Messaging.IntegrationTesting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.Kafka;

namespace LayerZero.Messaging.Kafka.IntegrationTests;

public sealed class KafkaTransportIntegrationTests(KafkaFixture fixture) : MessageTransportIntegrationTestBase, IClassFixture<KafkaFixture>
{
    protected override string BrokerName => "kafka";

    protected override IHost CreateHost(string applicationName, IntegrationState? state = null)
    {
        return IntegrationTestHost.Build(
            applicationName,
            builder => builder.AddKafkaBus("primary", options =>
            {
                options.BootstrapServers = fixture.Container.GetBootstrapAddress();
                options.PollInterval = TimeSpan.FromMilliseconds(100);
                options.PartitionCount = 3;
                options.MaxDeliveryAttempts = 2;
            }),
            configureServices: services =>
            {
                if (state is not null)
                {
                    services.AddSingleton(state);
                }
            });
    }
}

public sealed class KafkaFixture : TestcontainerFixtureBase<KafkaContainer>
{
    public KafkaFixture()
        : base("LayerZero.Messaging.Kafka.IntegrationTests", "kafka")
    {
    }

    protected override ValueTask<KafkaContainer> CreateContainerAsync(TestcontainerFixtureMetadata metadata)
    {
        return ValueTask.FromResult(
            ApplyContainerDefaults(new KafkaBuilder("confluentinc/cp-kafka:7.5.12"))
                .WithKRaft()
                .Build());
    }
}
