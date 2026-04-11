using LayerZero.Messaging.IntegrationTesting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;

namespace LayerZero.Messaging.RabbitMq.IntegrationTests;

public sealed class RabbitMqTransportIntegrationTests(RabbitMqFixture fixture) : MessageTransportIntegrationTestBase, IClassFixture<RabbitMqFixture>
{
    protected override string BrokerName => "rabbitmq";

    protected override IHost CreateHost(string applicationName, IntegrationState? state = null)
    {
        return IntegrationTestHost.Build(
            applicationName,
            builder => builder.AddRabbitMqBus("primary", options =>
            {
                options.ConnectionString = fixture.Container.GetConnectionString();
                options.PrefetchCount = 8;
                options.RetryDelay = TimeSpan.FromMilliseconds(200);
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

public sealed class RabbitMqFixture : IAsyncLifetime, IAsyncDisposable
{
    public RabbitMqContainer Container { get; private set; } = null!;

    public async ValueTask DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask InitializeAsync()
    {
        Container = new RabbitMqBuilder("rabbitmq:3.11").Build();
        await Container.StartAsync().ConfigureAwait(false);
    }
}
