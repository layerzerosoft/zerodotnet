namespace LayerZero.Testcontainers.Cleanup;

internal sealed record CleanupPlan(
    CleanupMode Mode,
    TimeSpan OlderThan,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<CleanupSessionPlan> Sessions)
{
    public IReadOnlyList<DockerResourceRecord> Resources =>
        Sessions
            .SelectMany(static session => session.Resources)
            .DistinctBy(static resource => (resource.Kind, resource.Id))
            .OrderBy(static resource => resource.Kind)
            .ThenBy(static resource => resource.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
