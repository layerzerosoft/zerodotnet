namespace LayerZero.Messaging.IntegrationTesting;

internal static class TestcontainerFixtureLogging
{
    private const string SessionIdLabel = "org.testcontainers.session-id";

    public static async Task LogStartedContainerAsync(
        string projectName,
        string brokerName,
        string runId,
        string containerId,
        string containerName,
        CancellationToken cancellationToken = default)
    {
        var labels = await TestcontainerDockerInspector.GetLabelsAsync(containerId, cancellationToken).ConfigureAwait(false);
        var sessionId = labels.TryGetValue(SessionIdLabel, out var labelValue)
            ? labelValue
            : "unknown";

        Console.WriteLine(
            $"[LayerZero.Testcontainers] project={projectName} broker={brokerName} runId={runId} container={containerName} id={containerId} sessionId={sessionId}");
    }
}
