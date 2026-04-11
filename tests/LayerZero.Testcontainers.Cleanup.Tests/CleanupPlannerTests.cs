namespace LayerZero.Testcontainers.Cleanup.Tests;

public sealed class CleanupPlannerTests
{
    [Fact]
    public void CreatePlan_filters_sessions_by_age_threshold()
    {
        var now = new DateTimeOffset(2026, 04, 11, 12, 00, 00, TimeSpan.Zero);
        var staleContainer = CreateResource(
            "broker-1",
            "rabbitmq-old",
            DockerResourceKind.Container,
            now.AddHours(-2),
            "session-old",
            includeRepositoryLabel: true);
        var recentContainer = CreateResource(
            "broker-2",
            "rabbitmq-new",
            DockerResourceKind.Container,
            now.AddMinutes(-5),
            "session-new",
            includeRepositoryLabel: true);

        var plan = CleanupPlanner.CreatePlan(
            CleanupMode.List,
            TimeSpan.FromMinutes(30),
            now,
            [staleContainer, recentContainer],
            [staleContainer, recentContainer]);

        Assert.Single(plan.Sessions);
        Assert.Equal("session-old", plan.Sessions[0].SessionId);
    }

    [Fact]
    public void CreatePlan_expands_session_resources_to_broker_and_ryuk()
    {
        var now = new DateTimeOffset(2026, 04, 11, 12, 00, 00, TimeSpan.Zero);
        var broker = CreateResource(
            "broker-1",
            "rabbitmq",
            DockerResourceKind.Container,
            now.AddHours(-2),
            "session-a",
            includeRepositoryLabel: true);
        var ryuk = CreateResource(
            "ryuk-1",
            "testcontainers-ryuk",
            DockerResourceKind.Container,
            now.AddHours(-2),
            "session-a",
            includeRepositoryLabel: false);
        var network = CreateResource(
            "network-1",
            "bridge-network",
            DockerResourceKind.Network,
            now.AddHours(-2),
            "session-a",
            includeRepositoryLabel: false);

        var plan = CleanupPlanner.CreatePlan(
            CleanupMode.List,
            TimeSpan.FromMinutes(30),
            now,
            [broker],
            [broker, ryuk, network]);

        Assert.Single(plan.Sessions);
        Assert.Equal(3, plan.Sessions[0].Resources.Count);
        Assert.Contains(plan.Sessions[0].Resources, resource => resource.Name == "testcontainers-ryuk");
        Assert.Contains(plan.Sessions[0].Resources, resource => resource.Name == "bridge-network");
    }

    [Fact]
    public void CreatePlan_allows_explicit_legacy_session_ids_without_repo_labels()
    {
        var now = new DateTimeOffset(2026, 04, 11, 12, 00, 00, TimeSpan.Zero);
        var legacyBroker = CreateResource(
            "broker-legacy",
            "rabbitmq-legacy",
            DockerResourceKind.Container,
            now.AddHours(-12),
            "legacy-session",
            includeRepositoryLabel: false);
        var legacyRyuk = CreateResource(
            "ryuk-legacy",
            "testcontainers-ryuk-legacy",
            DockerResourceKind.Container,
            now.AddHours(-12),
            "legacy-session",
            includeRepositoryLabel: false);

        var plan = CleanupPlanner.CreatePlan(
            CleanupMode.Apply,
            TimeSpan.FromMinutes(30),
            now,
            Array.Empty<DockerResourceRecord>(),
            [legacyBroker, legacyRyuk],
            ["legacy-session"]);

        var session = Assert.Single(plan.Sessions);
        Assert.Equal("legacy-session", session.SessionId);
        Assert.Empty(session.RepoOwnedContainers);
        Assert.Equal(2, session.Resources.Count);
    }

    private static DockerResourceRecord CreateResource(
        string id,
        string name,
        DockerResourceKind kind,
        DateTimeOffset createdAtUtc,
        string sessionId,
        bool includeRepositoryLabel)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CleanupLabels.SessionIdLabel] = sessionId,
        };

        if (includeRepositoryLabel)
        {
            labels[CleanupLabels.RepositoryLabel] = CleanupLabels.RepositoryName;
            labels[CleanupLabels.ProjectLabel] = "LayerZero.Testcontainers.Cleanup.Tests";
            labels[CleanupLabels.BrokerLabel] = "smoke";
            labels[CleanupLabels.RunIdLabel] = "run-1";
        }

        return new DockerResourceRecord(id, name, kind, createdAtUtc, labels);
    }
}
