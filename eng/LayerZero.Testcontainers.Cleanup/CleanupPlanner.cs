namespace LayerZero.Testcontainers.Cleanup;

internal static class CleanupPlanner
{
    public static CleanupPlan CreatePlan(
        CleanupMode mode,
        TimeSpan olderThan,
        DateTimeOffset nowUtc,
        IReadOnlyList<DockerResourceRecord> repoOwnedContainers,
        IReadOnlyList<DockerResourceRecord> sessionResources,
        IReadOnlyCollection<string>? explicitSessionIds = null)
    {
        ArgumentNullException.ThrowIfNull(repoOwnedContainers);
        ArgumentNullException.ThrowIfNull(sessionResources);

        var cutoff = nowUtc - olderThan;
        var resourceLookup = sessionResources
            .Where(static resource => !string.IsNullOrWhiteSpace(resource.SessionId))
            .GroupBy(static resource => resource.SessionId!, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<DockerResourceRecord>)group
                    .DistinctBy(static resource => (resource.Kind, resource.Id))
                    .OrderBy(static resource => resource.Kind)
                    .ThenBy(static resource => resource.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.Ordinal);

        var sessions = repoOwnedContainers
            .Where(static resource =>
                resource.Labels.TryGetValue(CleanupLabels.RepositoryLabel, out var repo)
                && string.Equals(repo, CleanupLabels.RepositoryName, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(resource.SessionId)
                && resource.CreatedAtUtc is not null)
            .GroupBy(static resource => resource.SessionId!, StringComparer.Ordinal)
            .Select(group =>
            {
                var latestCreatedAt = group.Max(static resource => resource.CreatedAtUtc!.Value);
                return new
                {
                    SessionId = group.Key,
                    LatestCreatedAt = latestCreatedAt,
                    Containers = (IReadOnlyList<DockerResourceRecord>)group
                        .OrderBy(static resource => resource.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                };
            })
            .Where(candidate => candidate.LatestCreatedAt <= cutoff)
            .Select(candidate =>
            {
                resourceLookup.TryGetValue(candidate.SessionId, out var resources);
                resources ??= candidate.Containers;

                return new CleanupSessionPlan(
                    candidate.SessionId,
                    candidate.LatestCreatedAt,
                    candidate.Containers,
                    resources);
            })
            .ToList();

        if (explicitSessionIds is not null)
        {
            foreach (var sessionId in explicitSessionIds.Distinct(StringComparer.Ordinal))
            {
                if (sessions.Any(existing => string.Equals(existing.SessionId, sessionId, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!resourceLookup.TryGetValue(sessionId, out var resources))
                {
                    continue;
                }

                var latestCreatedAt = resources
                    .Where(static resource => resource.CreatedAtUtc is not null)
                    .Select(static resource => resource.CreatedAtUtc!.Value)
                    .DefaultIfEmpty(DateTimeOffset.MaxValue)
                    .Max();

                if (latestCreatedAt == DateTimeOffset.MaxValue || latestCreatedAt > cutoff)
                {
                    continue;
                }

                sessions.Add(new CleanupSessionPlan(
                    sessionId,
                    latestCreatedAt,
                    Array.Empty<DockerResourceRecord>(),
                    resources));
            }
        }

        var orderedSessions = sessions
            .OrderBy(static session => session.LatestRepoOwnedContainerUtc)
            .ThenBy(static session => session.SessionId, StringComparer.Ordinal)
            .ToArray();

        return new CleanupPlan(mode, olderThan, nowUtc, orderedSessions);
    }
}
