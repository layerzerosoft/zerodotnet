using DotNet.Testcontainers.Builders;
using LayerZero.Messaging.IntegrationTesting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.Nats;

namespace LayerZero.Messaging.Nats.IntegrationTests;

public sealed class NatsTransportIntegrationTests(NatsFixture fixture) : MessageTransportIntegrationTestBase, IClassFixture<NatsFixture>
{
    protected override string BrokerName => "nats";

    protected override IHost CreateHost(string applicationName, IntegrationState? state = null)
    {
        return IntegrationTestHost.Build(
            applicationName,
            builder => builder.AddNatsBus("primary", options =>
            {
                options.Url = fixture.Container.GetConnectionString();
                options.RetryDelay = TimeSpan.FromMilliseconds(200);
                options.MaxDeliver = 2;
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

public sealed class NatsFixture : IAsyncLifetime, IAsyncDisposable
{
    public NatsContainer Container { get; private set; } = null!;

    public async ValueTask DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask InitializeAsync()
    {
        Container = new NatsBuilder("nats:2.9")
            .WithCommand("-js")
            .Build();
        await Container.StartAsync().ConfigureAwait(false);
    }
}
