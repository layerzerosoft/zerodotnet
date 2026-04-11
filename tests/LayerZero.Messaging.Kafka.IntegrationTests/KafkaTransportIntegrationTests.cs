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

public sealed class KafkaFixture : IAsyncLifetime, IAsyncDisposable
{
    public KafkaContainer Container { get; private set; } = null!;

    public async ValueTask DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask InitializeAsync()
    {
        Container = new KafkaBuilder("confluentinc/cp-kafka:7.5.12")
            .WithKRaft()
            .Build();
        await Container.StartAsync().ConfigureAwait(false);
    }
}
