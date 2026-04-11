namespace LayerZero.Testcontainers.Cleanup;

internal sealed record CleanupSessionPlan(
    string SessionId,
    DateTimeOffset LatestRepoOwnedContainerUtc,
    IReadOnlyList<DockerResourceRecord> RepoOwnedContainers,
    IReadOnlyList<DockerResourceRecord> Resources);
