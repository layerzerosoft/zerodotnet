extern alias FulfillmentAzureServiceBusAppHost;
extern alias FulfillmentKafkaAppHost;
extern alias FulfillmentNatsAppHost;
extern alias FulfillmentRabbitMqAppHost;

using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using LayerZero.Fulfillment.Contracts.Orders;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

[Trait("Category", "MatrixOnly")]
public sealed class FulfillmentAppHostStartupTests
{
    private static readonly TimeSpan AsyncFlowTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

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
        "Overriding HTTP_PORTS",
        "Binding to values defined by URLS instead",
    ];

    [Fact]
    public Task Rabbitmq_app_host_starts_fulfillment_resources_and_serves_real_api_flows()
    {
        return AssertAppHostStartsAsync<FulfillmentRabbitMqAppHost::Projects.LayerZero_Fulfillment_RabbitMq_AppHost>(
            brokerResources: ["rabbitmq", "postgres", "fulfillment"],
            oneShotResources: ["fulfillment-bootstrap"],
            longRunningResources: ["fulfillment-processing", "fulfillment-projections", "fulfillment-api"]);
    }

    [Fact]
    public Task Azure_service_bus_app_host_starts_fulfillment_resources_and_serves_real_api_flows()
    {
        return AssertAppHostStartsAsync<FulfillmentAzureServiceBusAppHost::Projects.LayerZero_Fulfillment_AzureServiceBus_AppHost>(
            brokerResources: ["servicebus", "postgres", "fulfillment"],
            oneShotResources: ["fulfillment-bootstrap"],
            longRunningResources: ["fulfillment-processing", "fulfillment-projections", "fulfillment-api"]);
    }

    [Fact]
    public Task Kafka_app_host_starts_fulfillment_resources_and_serves_real_api_flows()
    {
        return AssertAppHostStartsAsync<FulfillmentKafkaAppHost::Projects.LayerZero_Fulfillment_Kafka_AppHost>(
            brokerResources: ["kafka", "postgres", "fulfillment"],
            oneShotResources: ["fulfillment-kafka-readiness", "fulfillment-bootstrap"],
            longRunningResources: ["fulfillment-processing", "fulfillment-projections", "fulfillment-api"]);
    }

    [Fact]
    public Task Nats_app_host_starts_fulfillment_resources_and_serves_real_api_flows()
    {
        return AssertAppHostStartsAsync<FulfillmentNatsAppHost::Projects.LayerZero_Fulfillment_Nats_AppHost>(
            brokerResources: ["nats", "postgres", "fulfillment"],
            oneShotResources: ["fulfillment-bootstrap"],
            longRunningResources: ["fulfillment-processing", "fulfillment-projections", "fulfillment-api"]);
    }

    private static async Task AssertAppHostStartsAsync<TAppHost>(
        IReadOnlyList<string> brokerResources,
        IReadOnlyList<string> oneShotResources,
        IReadOnlyList<string> longRunningResources)
        where TAppHost : class
    {
        const string apiResourceName = "fulfillment-api";
        const string bootstrapResourceName = "fulfillment-bootstrap";

        var cancellationToken = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<TAppHost>(
            [],
            static (applicationOptions, _) => applicationOptions.EnableResourceLogging = true,
            cancellationToken);

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        foreach (var resourceName in brokerResources)
        {
            await app.ResourceNotifications.WaitForResourceHealthyAsync(
                resourceName,
                WaitBehavior.StopOnResourceUnavailable,
                cancellationToken);
        }

        foreach (var resourceName in oneShotResources)
        {
            var resourceEvent = await WaitForSuccessfulCompletionAsync(app, resourceName, cancellationToken);
            await AssertCompletedSuccessfullyAsync(app, resourceEvent, cancellationToken);
        }

        foreach (var resourceName in longRunningResources)
        {
            var resourceEvent = await WaitForRunningAsync(app, resourceName, cancellationToken);
            await AssertResourceRunningAsync(app, resourceEvent, cancellationToken);
        }

        var fulfillmentResources = oneShotResources.Concat(longRunningResources).ToArray();
        AssertResourcesNotFailed(app, fulfillmentResources);
        await AssertNoUnexpectedFailuresDuringStartupSoakAsync(app, fulfillmentResources, longRunningResources, TimeSpan.FromSeconds(5), cancellationToken);
        await AssertLongRunningResourcesRemainRunningAsync(app, longRunningResources, cancellationToken);
        AssertPublicApiEndpoints(app, apiResourceName);
        await AssertOpenApiReachableAsync(app, apiResourceName, cancellationToken);
        await AssertInvalidOrderRejectedAsync(app, apiResourceName, cancellationToken);
        await AssertHappyPathAsync(app, apiResourceName, cancellationToken);
        await AssertRetryFlowAsync(app, apiResourceName, cancellationToken);
        await AssertBootstrapLogsContainProvisioningFeedbackAsync(app, bootstrapResourceName, cancellationToken);
        await AssertLogsDoNotContainStartupFailuresAsync(app, fulfillmentResources, cancellationToken);
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
        IEnumerable<string> longRunningResourceNames,
        TimeSpan soakDuration,
        CancellationToken cancellationToken)
    {
        using var soakCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        soakCancellation.CancelAfter(soakDuration);

        var trackedResources = new HashSet<string>(resourceNames, StringComparer.Ordinal);
        var longRunningResources = new HashSet<string>(longRunningResourceNames, StringComparer.Ordinal);
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

    private static void AssertPublicApiEndpoints(
        DistributedApplication app,
        string resourceName)
    {
        var httpEndpoint = NormalizeLoopbackEndpoint(app.GetEndpoint(resourceName, "http"));
        var httpsEndpoint = NormalizeLoopbackEndpoint(app.GetEndpoint(resourceName, "https"));

        Assert.True(httpEndpoint.IsLoopback, $"Expected the '{resourceName}' HTTP endpoint to be loopback, but found '{httpEndpoint}'.");
        Assert.True(httpsEndpoint.IsLoopback, $"Expected the '{resourceName}' HTTPS endpoint to be loopback, but found '{httpsEndpoint}'.");
        Assert.Equal("http", httpEndpoint.Scheme);
        Assert.Equal("https", httpsEndpoint.Scheme);
        Assert.True(httpEndpoint.Port > 0, $"Expected the '{resourceName}' HTTP endpoint to use a non-zero port.");
        Assert.True(httpsEndpoint.Port > 0, $"Expected the '{resourceName}' HTTPS endpoint to use a non-zero port.");
        Assert.NotEqual(httpEndpoint.Port, httpsEndpoint.Port);
    }

    private static async Task AssertOpenApiReachableAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken)
    {
        using var httpClient = app.CreateHttpClient(resourceName, "http");
        using var httpsClient = app.CreateHttpClient(resourceName, "https");

        await AssertOpenApiReachableAsync(httpClient, "HTTP", cancellationToken);
        await AssertOpenApiReachableAsync(httpsClient, "HTTPS", cancellationToken);
    }

    private static async Task AssertOpenApiReachableAsync(
        HttpClient client,
        string endpointDisplayName,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected {endpointDisplayName} OpenAPI to be reachable, but received {(int)response.StatusCode}.{Environment.NewLine}{body}");
        Assert.Contains("\"openapi\"", body, StringComparison.Ordinal);
    }

    private static async Task AssertInvalidOrderRejectedAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken)
    {
        using var client = app.CreateHttpClient(resourceName, "http");
        var invalidRequest = new PlaceOrderApi.Request(
            string.Empty,
            [],
            new ShippingAddress(string.Empty, string.Empty, "Riga", "LV", "LV-1010"),
            new OrderScenario());

        using var response = await client.PostAsJsonAsync(OrderRoutes.Collection, invalidRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("layerzero.validation.not_empty", body, StringComparison.Ordinal);
    }

    private static async Task AssertHappyPathAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken)
    {
        using var client = app.CreateHttpClient(resourceName, "http");

        var orderId = await PlaceOrderAsync(client, new OrderScenario(), cancellationToken);
        var immediateOrder = await GetOrderAsync(client, orderId, cancellationToken);
        Assert.Equal(orderId, immediateOrder.Id);

        var completedOrder = await WaitForOrderAsync(
            client,
            orderId,
            static order => order.Status == OrderStatuses.Completed,
            AsyncFlowTimeout,
            cancellationToken);

        var timeline = await WaitForTimelineAsync(
            client,
            orderId,
            static entries =>
                entries.Any(entry => entry.Step == "api.accepted")
                && entries.Any(entry => entry.Step == "order.completed")
                && entries.Any(entry => entry.Step == "projection.completed"),
            AsyncFlowTimeout,
            cancellationToken);

        Assert.True(completedOrder.InventoryReserved);
        Assert.True(completedOrder.PaymentAuthorized);
        Assert.NotNull(completedOrder.TrackingNumber);
        Assert.Contains(timeline, static entry => entry.Step == "order.completed");
        Assert.Contains(timeline, static entry => entry.Step == "projection.completed");
    }

    private static async Task AssertRetryFlowAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken)
    {
        using var client = app.CreateHttpClient(resourceName, "http");

        var orderId = await PlaceOrderAsync(client, new OrderScenario(ForcePaymentTimeoutOnce: true), cancellationToken);

        var completedOrder = await WaitForOrderAsync(
            client,
            orderId,
            static order => order.Status == OrderStatuses.Completed,
            AsyncFlowTimeout,
            cancellationToken);

        var timeline = await WaitForTimelineAsync(
            client,
            orderId,
            static entries => entries.Count(entry => entry.Step == "payment.authorization") >= 2
                && entries.Any(entry => entry.Step == "order.completed"),
            AsyncFlowTimeout,
            cancellationToken);

        Assert.Equal(OrderStatuses.Completed, completedOrder.Status);
        Assert.True(timeline.Count(entry => entry.Step == "payment.authorization") >= 2);
    }

    private static async Task AssertBootstrapLogsContainProvisioningFeedbackAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken)
    {
        var resourceLogs = await GetResourceLogsAsync(app, resourceName, cancellationToken);
        var hasStructuredBootstrapLogs =
            resourceLogs.Contains("LayerZero bootstrap step 'migrations' started.", StringComparison.Ordinal)
            && resourceLogs.Contains("LayerZero bootstrap step 'migrations' completed in", StringComparison.Ordinal)
            && resourceLogs.Contains("LayerZero bootstrap step 'messaging-provisioning' started.", StringComparison.Ordinal)
            && resourceLogs.Contains("LayerZero bootstrap step 'messaging-provisioning' completed in", StringComparison.Ordinal);

        var hasLegacySampleLogs =
            resourceLogs.Contains("Fulfillment migrations started.", StringComparison.Ordinal)
            && resourceLogs.Contains("Fulfillment migrations completed.", StringComparison.Ordinal)
            && resourceLogs.Contains("Fulfillment messaging topology provisioning started.", StringComparison.Ordinal)
            && resourceLogs.Contains("Fulfillment messaging topology provisioning completed.", StringComparison.Ordinal);

        if (hasStructuredBootstrapLogs || hasLegacySampleLogs)
        {
            return;
        }

        Assert.Contains(
            "Resource logs: <none captured>",
            resourceLogs,
            StringComparison.Ordinal);
    }

    private static async Task<Guid> PlaceOrderAsync(
        HttpClient client,
        OrderScenario scenario,
        CancellationToken cancellationToken)
    {
        var request = new PlaceOrderApi.Request(
            "customer@example.com",
            [new OrderItem("LZ-ASYNC", 1)],
            new ShippingAddress("LayerZero Customer", "1 Async Avenue", "Riga", "LV", "LV-1010"),
            scenario);

        using var response = await client.PostAsJsonAsync(OrderRoutes.Collection, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var accepted = await response.Content.ReadFromJsonAsync<PlaceOrderApi.Accepted>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The AppHost-backed order placement response was empty.");

        var expectedLocationSuffix = FormatGuidRoute(OrderRoutes.Resource, accepted.OrderId);
        var location = response.Headers.Location?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(location), $"Expected order placement to return a Location header.{Environment.NewLine}{body}");
        Assert.EndsWith(expectedLocationSuffix, location, StringComparison.OrdinalIgnoreCase);

        return accepted.OrderId;
    }

    private static async Task<OrderDetails> GetOrderAsync(
        HttpClient client,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(FormatGuidRoute(OrderRoutes.Resource, orderId), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected GET order to succeed for '{orderId}', but received {(int)response.StatusCode}.{Environment.NewLine}{body}");

        return await response.Content.ReadFromJsonAsync<OrderDetails>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The order payload for '{orderId}' was empty.");
    }

    private static async Task<OrderDetails> WaitForOrderAsync(
        HttpClient client,
        Guid orderId,
        Func<OrderDetails, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var order = await GetOrderAsync(client, orderId, cancellationToken).ConfigureAwait(false);
            if (predicate(order))
            {
                return order;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for order '{orderId}' to reach the expected state through the AppHost API.");
    }

    private static async Task<IReadOnlyList<OrderTimelineEntry>> WaitForTimelineAsync(
        HttpClient client,
        Guid orderId,
        Func<IReadOnlyList<OrderTimelineEntry>, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var response = await client.GetAsync(FormatGuidRoute(OrderRoutes.Timeline, orderId), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            Assert.True(
                response.IsSuccessStatusCode,
                $"Expected GET timeline to succeed for '{orderId}', but received {(int)response.StatusCode}.{Environment.NewLine}{body}");

            var timeline = await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderTimelineEntry>>(cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
            if (predicate(timeline))
            {
                return timeline;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for timeline updates for order '{orderId}' through the AppHost API.");
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

    private static Uri NormalizeLoopbackEndpoint(Uri endpoint)
    {
        if (!endpoint.IsLoopback)
        {
            return endpoint;
        }

        var builder = new UriBuilder(endpoint)
        {
            Host = "localhost",
        };

        return builder.Uri;
    }

    private static string FormatGuidRoute(string routeTemplate, Guid orderId)
    {
        return routeTemplate.Replace("{id:guid}", orderId.ToString(), StringComparison.Ordinal);
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
