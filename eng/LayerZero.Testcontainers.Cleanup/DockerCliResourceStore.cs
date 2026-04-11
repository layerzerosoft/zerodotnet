namespace LayerZero.Testcontainers.Cleanup;

internal sealed class DockerCliResourceStore(DockerProcessRunner processRunner) : IDockerResourceStore
{
    private readonly DockerProcessRunner processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<IReadOnlyList<DockerResourceRecord>> ListRepoOwnedContainersAsync(string repositoryName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);

        var ids = await ListIdentifiersAsync(
            [
                "ps",
                "-a",
                "--no-trunc",
                "--filter",
                $"label={CleanupLabels.RepositoryLabel}={repositoryName}",
                "--format",
                "{{.ID}}",
            ],
            cancellationToken).ConfigureAwait(false);

        return await InspectAsync(DockerResourceKind.Container, ids, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DockerResourceRecord>> ListResourcesBySessionAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        if (sessionIds.Count == 0)
        {
            return Array.Empty<DockerResourceRecord>();
        }

        var containerIds = new HashSet<string>(StringComparer.Ordinal);
        var networkIds = new HashSet<string>(StringComparer.Ordinal);
        var volumeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sessionId in sessionIds)
        {
            foreach (var id in await ListIdentifiersAsync(
                         [
                             "ps",
                             "-a",
                             "--no-trunc",
                             "--filter",
                             $"label={CleanupLabels.SessionIdLabel}={sessionId}",
                             "--format",
                             "{{.ID}}",
                         ],
                         cancellationToken).ConfigureAwait(false))
            {
                containerIds.Add(id);
            }

            foreach (var id in await ListIdentifiersAsync(
                         [
                             "network",
                             "ls",
                             "-q",
                             "--filter",
                             $"label={CleanupLabels.SessionIdLabel}={sessionId}",
                         ],
                         cancellationToken).ConfigureAwait(false))
            {
                networkIds.Add(id);
            }

            foreach (var id in await ListIdentifiersAsync(
                         [
                             "volume",
                             "ls",
                             "-q",
                             "--filter",
                             $"label={CleanupLabels.SessionIdLabel}={sessionId}",
                         ],
                         cancellationToken).ConfigureAwait(false))
            {
                volumeIds.Add(id);
            }
        }

        var resources = new List<DockerResourceRecord>();
        resources.AddRange(await InspectAsync(DockerResourceKind.Container, containerIds, cancellationToken).ConfigureAwait(false));
        resources.AddRange(await InspectAsync(DockerResourceKind.Network, networkIds, cancellationToken).ConfigureAwait(false));
        resources.AddRange(await InspectAsync(DockerResourceKind.Volume, volumeIds, cancellationToken).ConfigureAwait(false));
        return resources;
    }

    public async Task RemoveResourcesAsync(IReadOnlyList<DockerResourceRecord> resources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);

        foreach (var resource in resources.OrderBy(static resource => resource.Kind).ThenBy(static resource => resource.Name, StringComparer.OrdinalIgnoreCase))
        {
            var arguments = resource.Kind switch
            {
                DockerResourceKind.Container => new[] { "rm", "-f", resource.Id },
                DockerResourceKind.Network => new[] { "network", "rm", resource.Id },
                DockerResourceKind.Volume => new[] { "volume", "rm", resource.Id },
                _ => throw new ArgumentOutOfRangeException(nameof(resource.Kind), resource.Kind, "Unsupported Docker resource kind."),
            };

            var result = await processRunner.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess || IsNotFound(result.StandardError))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Failed to remove {resource.Kind} '{resource.Name}' ({resource.Id}). Docker said: {result.StandardError.Trim()}");
        }
    }

    private async Task<IReadOnlyList<DockerResourceRecord>> InspectAsync(
        DockerResourceKind kind,
        IReadOnlyCollection<string> identifiers,
        CancellationToken cancellationToken)
    {
        if (identifiers.Count == 0)
        {
            return Array.Empty<DockerResourceRecord>();
        }

        var arguments = new List<string>();
        switch (kind)
        {
            case DockerResourceKind.Container:
                arguments.Add("inspect");
                break;
            case DockerResourceKind.Network:
                arguments.AddRange(["network", "inspect"]);
                break;
            case DockerResourceKind.Volume:
                arguments.AddRange(["volume", "inspect"]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Docker resource kind.");
        }

        arguments.AddRange(identifiers);

        var result = await processRunner.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"docker {string.Join(' ', arguments)} failed: {result.StandardError.Trim()}");
        }

        return DockerInspectParser.Parse(result.StandardOutput, kind);
    }

    private async Task<IReadOnlyList<string>> ListIdentifiersAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"docker {string.Join(' ', arguments)} failed: {result.StandardError.Trim()}");
        }

        return result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsNotFound(string stderr)
    {
        return stderr.Contains("No such", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }
}
