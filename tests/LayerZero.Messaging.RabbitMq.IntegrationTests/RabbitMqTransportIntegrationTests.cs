using LayerZero.Messaging.IntegrationTesting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;

namespace LayerZero.Messaging.RabbitMq.IntegrationTests;

public sealed class RabbitMqTransportIntegrationTests(RabbitMqFixture fixture) : MessageTransportIntegrationTestBase, IClassFixture<RabbitMqFixture>
{
    public new static bool SkipWhenCloudEnvironmentUnavailable => false;

    protected override string BrokerName => "rabbitmq";

    [Fact]
    public async Task Fixture_applies_repo_owned_testcontainer_labels()
    {
        var labels = await fixture.GetContainerLabelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TestcontainerFixtureMetadata.RepositoryName, labels[TestcontainerFixtureMetadata.RepositoryLabel]);
        Assert.Equal("LayerZero.Messaging.RabbitMq.IntegrationTests", labels[TestcontainerFixtureMetadata.ProjectLabel]);
        Assert.Equal("rabbitmq", labels[TestcontainerFixtureMetadata.BrokerLabel]);
        Assert.Equal(fixture.RunId, labels[TestcontainerFixtureMetadata.RunIdLabel]);
        Assert.False(string.IsNullOrWhiteSpace(labels["org.testcontainers.session-id"]));
    }

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

public sealed class RabbitMqFixture : TestcontainerFixtureBase<RabbitMqContainer>
{
    public RabbitMqFixture()
        : base("LayerZero.Messaging.RabbitMq.IntegrationTests", "rabbitmq")
    {
    }

    protected override ValueTask<RabbitMqContainer> CreateContainerAsync(TestcontainerFixtureMetadata metadata)
    {
        return ValueTask.FromResult(
            ApplyContainerDefaults(new RabbitMqBuilder("rabbitmq:3.11"))
                .Build());
    }
}
