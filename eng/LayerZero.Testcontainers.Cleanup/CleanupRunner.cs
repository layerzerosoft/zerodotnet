namespace LayerZero.Testcontainers.Cleanup;

internal sealed class CleanupRunner(
    IDockerResourceStore resourceStore,
    TextWriter stdout,
    Func<DateTimeOffset>? utcNow = null)
{
    private readonly IDockerResourceStore resourceStore = resourceStore ?? throw new ArgumentNullException(nameof(resourceStore));
    private readonly TextWriter stdout = stdout ?? throw new ArgumentNullException(nameof(stdout));
    private readonly Func<DateTimeOffset> utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

    public async Task<int> RunAsync(CleanupArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var repoOwnedContainers = await resourceStore.ListRepoOwnedContainersAsync(CleanupLabels.RepositoryName, cancellationToken).ConfigureAwait(false);
        var sessionIds = repoOwnedContainers
            .Select(static resource => resource.SessionId)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Concat(arguments.SessionIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var sessionResources = await resourceStore.ListResourcesBySessionAsync(sessionIds, cancellationToken).ConfigureAwait(false);
        var plan = CleanupPlanner.CreatePlan(arguments.Mode, arguments.OlderThan, utcNow(), repoOwnedContainers, sessionResources, arguments.SessionIds);

        WritePlan(plan);

        if (plan.Sessions.Count == 0)
        {
            return 0;
        }

        if (arguments.Mode == CleanupMode.Apply)
        {
            await resourceStore.RemoveResourcesAsync(plan.Resources, cancellationToken).ConfigureAwait(false);
            await stdout.WriteLineAsync(
                $"Removed {plan.Resources.Count} Docker resource(s) across {plan.Sessions.Count} stale LayerZero Testcontainers session(s).")
                .ConfigureAwait(false);
        }

        return 0;
    }

    private void WritePlan(CleanupPlan plan)
    {
        stdout.WriteLine(
            $"Mode={plan.Mode} olderThan={plan.OlderThan} generatedAtUtc={plan.GeneratedAtUtc:O}");

        if (plan.Sessions.Count == 0)
        {
            stdout.WriteLine("No stale LayerZero Testcontainers sessions matched the cleanup criteria.");
            return;
        }

        foreach (var session in plan.Sessions)
        {
            stdout.WriteLine(
                $"Session {session.SessionId} latestRepoOwnedContainerUtc={session.LatestRepoOwnedContainerUtc:O}");

            foreach (var resource in session.Resources)
            {
                stdout.WriteLine(
                    $"  {resource.Kind} {resource.Name} ({resource.Id})");
            }
        }

        if (plan.Mode == CleanupMode.List)
        {
            stdout.WriteLine("Dry-run only. Re-run with --apply to remove these stale resources.");
        }
    }
}
