using System.Diagnostics;
using DotNet.Testcontainers.Builders;

namespace LayerZero.Testcontainers.Cleanup.Tests;

public sealed class CleanupDockerSmokeTests
{
    [Fact]
    public async Task Cleanup_runner_lists_and_removes_a_stale_labeled_container()
    {
        var sessionId = $"smoke-{Guid.NewGuid():N}";
        var runId = Guid.NewGuid().ToString("N");

        var container = new ContainerBuilder("alpine:3.20")
            .WithCommand(["sh", "-c", "sleep 600"])
            .WithLabel(CleanupLabels.RepositoryLabel, CleanupLabels.RepositoryName)
            .WithLabel(CleanupLabels.ProjectLabel, "LayerZero.Testcontainers.Cleanup.Tests")
            .WithLabel(CleanupLabels.BrokerLabel, "smoke")
            .WithLabel(CleanupLabels.RunIdLabel, runId)
            .WithLabel(CleanupLabels.SessionIdLabel, sessionId)
            .Build();

        await container.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            var resourceStore = new ScopedDockerResourceStore(
                new DockerCliResourceStore(new DockerProcessRunner()),
                runId);

            var listOutput = new StringWriter();
            var listRunner = new CleanupRunner(
                resourceStore,
                listOutput,
                () => DateTimeOffset.UtcNow);

            var listExitCode = await listRunner.RunAsync(
                new CleanupArguments(CleanupMode.List, TimeSpan.Zero, Array.Empty<string>()),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, listExitCode);
            Assert.Contains(container.Id, listOutput.ToString(), StringComparison.Ordinal);

            var applyOutput = new StringWriter();
            var applyRunner = new CleanupRunner(
                resourceStore,
                applyOutput,
                () => DateTimeOffset.UtcNow);

            var applyExitCode = await applyRunner.RunAsync(
                new CleanupArguments(CleanupMode.Apply, TimeSpan.Zero, Array.Empty<string>()),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, applyExitCode);
            Assert.DoesNotContain(container.Id, await ListRunningContainerIdsAsync(container.Id), StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                await container.DisposeAsync();
            }
            catch
            {
            }
        }
    }

    private static async Task<string> ListRunningContainerIdsAsync(string id)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("ps");
        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add($"id={id}");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("{{.ID}}");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start docker ps.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return await stdout;
    }

    private sealed class ScopedDockerResourceStore(IDockerResourceStore innerStore, string runId) : IDockerResourceStore
    {
        private readonly IDockerResourceStore innerStore = innerStore;
        private readonly string runId = runId;

        public async Task<IReadOnlyList<DockerResourceRecord>> ListRepoOwnedContainersAsync(string repositoryName, CancellationToken cancellationToken)
        {
            var resources = await innerStore.ListRepoOwnedContainersAsync(repositoryName, cancellationToken);
            return resources
                .Where(resource =>
                    resource.Labels.TryGetValue(CleanupLabels.ProjectLabel, out var project)
                    && string.Equals(project, "LayerZero.Testcontainers.Cleanup.Tests", StringComparison.Ordinal)
                    && resource.Labels.TryGetValue(CleanupLabels.RunIdLabel, out var labeledRunId)
                    && string.Equals(labeledRunId, runId, StringComparison.Ordinal))
                .ToArray();
        }

        public Task<IReadOnlyList<DockerResourceRecord>> ListResourcesBySessionAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken)
        {
            return innerStore.ListResourcesBySessionAsync(sessionIds, cancellationToken);
        }

        public Task RemoveResourcesAsync(IReadOnlyList<DockerResourceRecord> resources, CancellationToken cancellationToken)
        {
            return innerStore.RemoveResourcesAsync(resources, cancellationToken);
        }
    }
}
