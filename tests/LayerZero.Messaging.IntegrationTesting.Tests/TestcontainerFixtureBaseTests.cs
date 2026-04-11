using LayerZero.Messaging.IntegrationTesting;

namespace LayerZero.Messaging.IntegrationTesting.Tests;

public sealed class TestcontainerFixtureBaseTests
{
    [Fact]
    public async Task DisposeAsync_is_idempotent()
    {
        var fixture = new FakeFixture();

        await fixture.InitializeAsync();
        await fixture.DisposeAsync();
        await fixture.DisposeAsync();

        Assert.Equal(1, fixture.StartCalls);
        Assert.Equal(1, fixture.StopCalls);
        Assert.Equal(1, fixture.DisposeCalls);
    }

    [Fact]
    public async Task InitializeAsync_cleans_up_created_container_when_start_fails()
    {
        var fixture = new FakeFixture { FailOnStart = true };

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.InitializeAsync());
        await fixture.DisposeAsync();

        Assert.Equal(1, fixture.StartCalls);
        Assert.Equal(1, fixture.StopCalls);
        Assert.Equal(1, fixture.DisposeCalls);
    }

    [Fact]
    public async Task Shutdown_cleanup_callback_is_idempotent()
    {
        var fixture = new FakeFixture();

        await fixture.InitializeAsync();

        fixture.TriggerShutdownCleanupForTests();
        fixture.TriggerShutdownCleanupForTests();
        await fixture.DisposeAsync();

        Assert.Equal(1, fixture.StopCalls);
        Assert.Equal(1, fixture.DisposeCalls);
    }

    private sealed class FakeFixture : TestcontainerFixtureBase<FakeContainer>
    {
        public FakeFixture()
            : base("LayerZero.Messaging.IntegrationTesting.Tests", "fake")
        {
        }

        public bool FailOnStart { get; init; }

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        protected override ValueTask<FakeContainer> CreateContainerAsync(TestcontainerFixtureMetadata metadata)
        {
            return ValueTask.FromResult(new FakeContainer());
        }

        protected override Task StartContainerAsync(FakeContainer container, CancellationToken cancellationToken)
        {
            StartCalls++;
            return FailOnStart
                ? Task.FromException(new InvalidOperationException("Boom."))
                : Task.CompletedTask;
        }

        protected override Task StopContainerAsync(FakeContainer container, CancellationToken cancellationToken)
        {
            StopCalls++;
            return Task.CompletedTask;
        }

        protected override ValueTask DisposeContainerAsync(FakeContainer container)
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask<ContainerDetails> GetContainerDetailsAsync(FakeContainer container)
        {
            return ValueTask.FromResult(new ContainerDetails("fake-id", "fake-name"));
        }
    }

    private sealed class FakeContainer;
}
