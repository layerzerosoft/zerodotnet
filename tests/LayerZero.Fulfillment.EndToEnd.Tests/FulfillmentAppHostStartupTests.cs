extern alias FulfillmentAppHost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

public sealed class FulfillmentAppHostStartupTests
{
    private static readonly string[] BrokerResources =
    [
        "rabbitmq",
        "sbemulatorns",
        "kafka",
        "nats",
    ];

    private static readonly string[] ReadinessResources =
    [
        "fulfillment-kafka-readiness",
    ];

    private static readonly string[] BootstrapResources =
    [
        "fulfillment-bootstrap-rabbitmq",
        "fulfillment-bootstrap-azureservicebus",
        "fulfillment-bootstrap-kafka",
        "fulfillment-bootstrap-nats",
    ];

    private static readonly string[] ApiResources =
    [
        "fulfillment-api-rabbitmq",
        "fulfillment-api-azureservicebus",
        "fulfillment-api-kafka",
        "fulfillment-api-nats",
    ];

    private static readonly string[] LongRunningResources =
    [
        "fulfillment-processing-rabbitmq",
        "fulfillment-projections-rabbitmq",
        "fulfillment-api-rabbitmq",
        "fulfillment-processing-azureservicebus",
        "fulfillment-projections-azureservicebus",
        "fulfillment-api-azureservicebus",
        "fulfillment-processing-kafka",
        "fulfillment-projections-kafka",
        "fulfillment-api-kafka",
        "fulfillment-processing-nats",
        "fulfillment-projections-nats",
        "fulfillment-api-nats",
    ];

    private static readonly string[] OneShotResources = [.. ReadinessResources, .. BootstrapResources];
    private static readonly string[] FulfillmentResources = [.. OneShotResources, .. LongRunningResources];

    private static readonly HashSet<string> StartupFailureStates = new(StringComparer.Ordinal)
    {
        KnownResourceStates.FailedToStart,
        KnownResourceStates.RuntimeUnhealthy,
    };

    private static readonly string[] UnexpectedLogFragments =
    [
        "Unhandled exception",
        "Hosting failed to start",
        "brokers are down",
        "All broker connections are down",
        "The response ended prematurely",
        "Status: 404 (Not Found)",
        "MessagingEntityNotFound",
    ];

    [Fact]
    public async Task App_host_starts_fulfillment_resources_without_runtime_failures()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<FulfillmentAppHost::Projects.LayerZero_Fulfillment_AppHost>(
            [],
            static (applicationOptions, _) => applicationOptions.EnableResourceLogging = true,
            cancellationToken);

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        foreach (var resourceName in BrokerResources)
        {
            await app.ResourceNotifications.WaitForResourceHealthyAsync(
                resourceName,
                WaitBehavior.StopOnResourceUnavailable,
                cancellationToken);
        }

        foreach (var resourceName in OneShotResources)
        {
            var resourceEvent = await WaitForSuccessfulCompletionAsync(app, resourceName, cancellationToken);
            await AssertCompletedSuccessfullyAsync(app, resourceEvent, cancellationToken);
        }

        foreach (var resourceName in LongRunningResources)
        {
            var resourceEvent = await WaitForRunningAsync(app, resourceName, cancellationToken);
            await AssertResourceRunningAsync(app, resourceEvent, cancellationToken);
        }

        AssertResourcesNotFailed(app, FulfillmentResources);
        await AssertNoUnexpectedFailuresDuringStartupSoakAsync(app, FulfillmentResources, TimeSpan.FromSeconds(5), cancellationToken);
        await AssertLongRunningResourcesRemainRunningAsync(app, LongRunningResources, cancellationToken);
        await AssertLogsDoNotContainStartupFailuresAsync(app, FulfillmentResources, cancellationToken);
        await AssertOpenApiReachableAsync(app, ApiResources, cancellationToken);
    }

    private static Task<ResourceEvent> WaitForSuccessfulCompletionAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken)
    {
        return app.ResourceNotifications.WaitForResourceAsync(
            resourceName,
            resourceEvent => IsCompletionState(resourceEvent) || IsStartupFailure(resourceEvent),
            cancellationToken);
    }

    private static Task<ResourceEvent> WaitForRunningAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken)
    {
        return app.ResourceNotifications.WaitForResourceAsync(
            resourceName,
            resourceEvent => IsRunningState(resourceEvent) || IsUnexpectedLongRunningState(resourceEvent),
            cancellationToken);
    }

    private static async Task AssertCompletedSuccessfullyAsync(
        DistributedApplication app,
        ResourceEvent resourceEvent,
        CancellationToken cancellationToken)
    {
        var state = resourceEvent.Snapshot.State?.Text;
        Assert.True(
            state == KnownResourceStates.Finished || state == KnownResourceStates.Exited,
            FormatFailureMessage(resourceEvent, $"Resource '{resourceEvent.Resource.Name}' did not complete successfully."));

        if (resourceEvent.Snapshot.ExitCode is { } exitCode)
        {
            var resourceLogs = await GetResourceLogsAsync(app, resourceEvent.Resource.Name, cancellationToken);
            Assert.True(
                exitCode == 0,
                FormatFailureMessage(
                    resourceEvent,
                    $"Resource '{resourceEvent.Resource.Name}' exited with code {exitCode}.{Environment.NewLine}{resourceLogs}"));
        }
    }

    private static async Task AssertResourceRunningAsync(
        DistributedApplication app,
        ResourceEvent resourceEvent,
        CancellationToken cancellationToken)
    {
        var state = resourceEvent.Snapshot.State?.Text ?? string.Empty;
        if (state == KnownResourceStates.Running)
        {
            return;
        }

        var resourceLogs = await GetResourceLogsAsync(app, resourceEvent.Resource.Name, cancellationToken);
        Assert.Fail(
            $"{FormatFailureMessage(resourceEvent, $"Resource '{resourceEvent.Resource.Name}' did not reach the running state.")}{Environment.NewLine}{resourceLogs}");
    }

    private static void AssertResourcesNotFailed(DistributedApplication app, IEnumerable<string> resourceNames)
    {
        foreach (var resourceName in resourceNames)
        {
            Assert.True(
                app.ResourceNotifications.TryGetCurrentState(resourceName, out var resourceEvent),
                $"Expected AppHost to publish state for resource '{resourceName}'.");

            Assert.NotNull(resourceEvent);
            Assert.False(
                IsUnexpectedFailure(resourceEvent!),
                FormatFailureMessage(resourceEvent!, $"Resource '{resourceName}' is in a failed state after startup."));
        }
    }

    private static async Task AssertNoUnexpectedFailuresDuringStartupSoakAsync(
        DistributedApplication app,
        IEnumerable<string> resourceNames,
        TimeSpan soakDuration,
        CancellationToken cancellationToken)
    {
        using var soakCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        soakCancellation.CancelAfter(soakDuration);

        var trackedResources = new HashSet<string>(resourceNames, StringComparer.Ordinal);
        var longRunningResources = new HashSet<string>(LongRunningResources, StringComparer.Ordinal);
        var failures = new List<string>();

        try
        {
            await foreach (var resourceEvent in app.ResourceNotifications.WatchAsync(soakCancellation.Token))
            {
                if (!trackedResources.Contains(resourceEvent.Resource.Name))
                {
                    continue;
                }

                if (!IsUnexpectedResourceState(resourceEvent, longRunningResources))
                {
                    continue;
                }

                var resourceLogs = await GetResourceLogsAsync(app, resourceEvent.Resource.Name, cancellationToken);
                failures.Add(
                    $"{FormatFailureMessage(resourceEvent, "Resource entered an unexpected state during the startup soak.")}{Environment.NewLine}{resourceLogs}");
                break;
            }
        }
        catch (OperationCanceledException) when (soakCancellation.IsCancellationRequested)
        {
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static async Task AssertLongRunningResourcesRemainRunningAsync(
        DistributedApplication app,
        IEnumerable<string> resourceNames,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();

        foreach (var resourceName in resourceNames)
        {
            Assert.True(
                app.ResourceNotifications.TryGetCurrentState(resourceName, out var resourceEvent),
                $"Expected AppHost to publish state for long-running resource '{resourceName}'.");

            Assert.NotNull(resourceEvent);
            if (IsRunningState(resourceEvent!))
            {
                continue;
            }

            var resourceLogs = await GetResourceLogsAsync(app, resourceName, cancellationToken);
            failures.Add(
                $"{FormatFailureMessage(resourceEvent!, $"Long-running resource '{resourceName}' is not running after startup.")}{Environment.NewLine}{resourceLogs}");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static async Task AssertLogsDoNotContainStartupFailuresAsync(
        DistributedApplication app,
        IEnumerable<string> resourceNames,
        CancellationToken cancellationToken)
    {
        var resourceLoggerService = app.Services.GetRequiredService<ResourceLoggerService>();
        var failures = new List<string>();

        foreach (var resourceName in resourceNames)
        {
            await foreach (var logBatch in resourceLoggerService.GetAllAsync(resourceName).WithCancellation(cancellationToken))
            {
                foreach (var logLine in logBatch)
                {
                    if (!ContainsUnexpectedLog(logLine.Content))
                    {
                        continue;
                    }

                    failures.Add($"{resourceName}: {logLine.Content}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static async Task AssertOpenApiReachableAsync(
        DistributedApplication app,
        IEnumerable<string> apiResources,
        CancellationToken cancellationToken)
    {
        foreach (var resourceName in apiResources)
        {
            using var client = app.CreateHttpClient(resourceName, "http");
            using var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

            Assert.True(
                response.IsSuccessStatusCode,
                $"Expected resource '{resourceName}' to serve /openapi/v1.json successfully, but received {(int)response.StatusCode}.");
        }
    }

    private static bool ContainsUnexpectedLog(string content)
    {
        foreach (var fragment in UnexpectedLogFragments)
        {
            if (content.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnexpectedResourceState(
        ResourceEvent resourceEvent,
        IReadOnlySet<string> longRunningResources)
    {
        if (longRunningResources.Contains(resourceEvent.Resource.Name))
        {
            return IsUnexpectedLongRunningState(resourceEvent);
        }

        return IsUnexpectedFailure(resourceEvent);
    }

    private static bool IsUnexpectedLongRunningState(ResourceEvent resourceEvent)
    {
        return IsStartupFailure(resourceEvent) || IsCompletionState(resourceEvent);
    }

    private static async Task<string> GetResourceLogsAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken)
    {
        var resourceLoggerService = app.Services.GetRequiredService<ResourceLoggerService>();
        var lines = new List<string>();

        await foreach (var logBatch in resourceLoggerService.GetAllAsync(resourceName).WithCancellation(cancellationToken))
        {
            foreach (var logLine in logBatch)
            {
                lines.Add(logLine.Content);
            }
        }

        var resourceLogs = lines.Count == 0
            ? "Resource logs: <none captured>"
            : $"Resource logs:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";

        var tempFileLogs = TryGetAspireTempFileLogs(resourceName);
        return string.IsNullOrWhiteSpace(tempFileLogs)
            ? resourceLogs
            : $"{resourceLogs}{Environment.NewLine}{tempFileLogs}";
    }

    private static string? TryGetAspireTempFileLogs(string resourceName)
    {
        var tempRoot = Path.GetTempPath();
        if (!Directory.Exists(tempRoot))
        {
            return null;
        }

        var latestAspireDirectory = Directory.EnumerateDirectories(tempRoot, "aspire.*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (latestAspireDirectory is null)
        {
            return null;
        }

        var matchingFiles = Directory.EnumerateFiles(latestAspireDirectory, $"{resourceName}*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (matchingFiles.Length == 0)
        {
            return $"Aspire temp files: <none found for '{resourceName}' in '{latestAspireDirectory}'>";
        }

        var sections = new List<string>(matchingFiles.Length);
        foreach (var filePath in matchingFiles)
        {
            var content = File.ReadAllText(filePath);
            sections.Add(
                string.IsNullOrWhiteSpace(content)
                    ? $"{Path.GetFileName(filePath)}: <empty>"
                    : $"{Path.GetFileName(filePath)}:{Environment.NewLine}{content}");
        }

        return $"Aspire temp files ({latestAspireDirectory}):{Environment.NewLine}{string.Join(Environment.NewLine, sections)}";
    }

    private static bool IsStartupFailure(ResourceEvent resourceEvent)
    {
        return resourceEvent.Snapshot.State?.Text is { } state && StartupFailureStates.Contains(state);
    }

    private static bool IsCompletionState(ResourceEvent resourceEvent)
    {
        var state = resourceEvent.Snapshot.State?.Text;
        return state == KnownResourceStates.Finished || state == KnownResourceStates.Exited;
    }

    private static bool IsRunningState(ResourceEvent resourceEvent)
    {
        return resourceEvent.Snapshot.State?.Text == KnownResourceStates.Running;
    }

    private static bool IsUnexpectedFailure(ResourceEvent resourceEvent)
    {
        return IsStartupFailure(resourceEvent)
            || resourceEvent.Snapshot.State?.Text == KnownResourceStates.Exited && resourceEvent.Snapshot.ExitCode is not null and not 0;
    }

    private static string FormatFailureMessage(ResourceEvent resourceEvent, string message)
    {
        var state = resourceEvent.Snapshot.State?.Text ?? "<unknown>";
        var exitCode = resourceEvent.Snapshot.ExitCode?.ToString() ?? "<null>";
        return $"{message} Current state: {state}. Exit code: {exitCode}.";
    }
}
