namespace LayerZero.Testcontainers.Cleanup.Tests;

public sealed class CleanupRunnerTests
{
    [Fact]
    public async Task List_mode_does_not_remove_resources()
    {
        var createdAt = new DateTimeOffset(2026, 04, 11, 10, 00, 00, TimeSpan.Zero);
        var broker = CreateRepoContainer("broker-1", "rabbitmq", createdAt, "session-1");
        var store = new FakeDockerResourceStore([broker], [broker]);
        var writer = new StringWriter();
        var runner = new CleanupRunner(store, writer, () => new DateTimeOffset(2026, 04, 11, 12, 00, 00, TimeSpan.Zero));

        var exitCode = await runner.RunAsync(
            new CleanupArguments(CleanupMode.List, TimeSpan.FromMinutes(30), Array.Empty<string>()),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, store.RemoveCalls);
        Assert.Contains("Dry-run only", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_mode_removes_planned_resources()
    {
        var createdAt = new DateTimeOffset(2026, 04, 11, 10, 00, 00, TimeSpan.Zero);
        var broker = CreateRepoContainer("broker-1", "rabbitmq", createdAt, "session-1");
        var ryuk = new DockerResourceRecord(
            "ryuk-1",
            "testcontainers-ryuk",
            DockerResourceKind.Container,
            createdAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CleanupLabels.SessionIdLabel] = "session-1",
            });

        var store = new FakeDockerResourceStore([broker], [broker, ryuk]);
        var writer = new StringWriter();
        var runner = new CleanupRunner(store, writer, () => new DateTimeOffset(2026, 04, 11, 12, 00, 00, TimeSpan.Zero));

        var exitCode = await runner.RunAsync(
            new CleanupArguments(CleanupMode.Apply, TimeSpan.FromMinutes(30), Array.Empty<string>()),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, store.RemoveCalls);
        Assert.Equal(2, store.RemovedResources.Count);
        Assert.Contains("Removed 2 Docker resource(s)", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_mode_can_target_an_explicit_legacy_session()
    {
        var createdAt = new DateTimeOffset(2026, 04, 11, 10, 00, 00, TimeSpan.Zero);
        var legacyBroker = new DockerResourceRecord(
            "legacy-broker",
            "rabbitmq-legacy",
            DockerResourceKind.Container,
            createdAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CleanupLabels.SessionIdLabel] = "legacy-session",
            });

        var store = new FakeDockerResourceStore(Array.Empty<DockerResourceRecord>(), [legacyBroker]);
        var writer = new StringWriter();
        var runner = new CleanupRunner(store, writer, () => new DateTimeOffset(2026, 04, 11, 12, 00, 00, TimeSpan.Zero));

        var exitCode = await runner.RunAsync(
            new CleanupArguments(CleanupMode.Apply, TimeSpan.FromMinutes(30), ["legacy-session"]),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, store.RemoveCalls);
        Assert.Single(store.RemovedResources);
    }

    private static DockerResourceRecord CreateRepoContainer(string id, string name, DateTimeOffset createdAtUtc, string sessionId)
    {
        return new DockerResourceRecord(
            id,
            name,
            DockerResourceKind.Container,
            createdAtUtc,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CleanupLabels.RepositoryLabel] = CleanupLabels.RepositoryName,
                [CleanupLabels.ProjectLabel] = "LayerZero.Testcontainers.Cleanup.Tests",
                [CleanupLabels.BrokerLabel] = "smoke",
                [CleanupLabels.RunIdLabel] = "run-1",
                [CleanupLabels.SessionIdLabel] = sessionId,
            });
    }

    private sealed class FakeDockerResourceStore(
        IReadOnlyList<DockerResourceRecord> repoOwnedContainers,
        IReadOnlyList<DockerResourceRecord> sessionResources) : IDockerResourceStore
    {
        public int RemoveCalls { get; private set; }

        public IReadOnlyList<DockerResourceRecord> RemovedResources { get; private set; } = Array.Empty<DockerResourceRecord>();

        public Task<IReadOnlyList<DockerResourceRecord>> ListRepoOwnedContainersAsync(string repositoryName, CancellationToken cancellationToken)
        {
            return Task.FromResult(repoOwnedContainers);
        }

        public Task<IReadOnlyList<DockerResourceRecord>> ListResourcesBySessionAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken)
        {
            return Task.FromResult(sessionResources);
        }

        public Task RemoveResourcesAsync(IReadOnlyList<DockerResourceRecord> resources, CancellationToken cancellationToken)
        {
            RemoveCalls++;
            RemovedResources = resources.ToArray();
            return Task.CompletedTask;
        }
    }
}
