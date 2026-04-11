namespace LayerZero.Testcontainers.Cleanup;

internal interface IDockerResourceStore
{
    Task<IReadOnlyList<DockerResourceRecord>> ListRepoOwnedContainersAsync(string repositoryName, CancellationToken cancellationToken);

    Task<IReadOnlyList<DockerResourceRecord>> ListResourcesBySessionAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken);

    Task RemoveResourcesAsync(IReadOnlyList<DockerResourceRecord> resources, CancellationToken cancellationToken);
}
