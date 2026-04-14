using System.Diagnostics;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.Processing;
using LayerZero.Fulfillment.Projections;
using LayerZero.Fulfillment.Shared;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

public sealed class FulfillmentOrderRun
{
    public FulfillmentOrderRun(Guid orderId, string traceParent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceParent);

        OrderId = orderId;
        TraceParent = traceParent;
        TraceId = ExtractTraceId(traceParent);
    }

    public Guid OrderId { get; }

    public string TraceParent { get; }

    public string TraceId { get; }

    public bool MatchesTraceParent(string? traceParent)
    {
        var candidateTraceId = TryExtractTraceId(traceParent);
        return candidateTraceId is not null
            && string.Equals(TraceId, candidateTraceId, StringComparison.Ordinal);
    }

    private static string ExtractTraceId(string traceParent)
    {
        return TryExtractTraceId(traceParent)
            ?? throw new ArgumentException("Trace parent must be a valid W3C traceparent value.", nameof(traceParent));
    }

    private static string? TryExtractTraceId(string? traceParent)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return null;
        }

        var segments = traceParent.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 4 && segments[1].Length == 32
            ? segments[1]
            : null;
    }
}

public sealed class FulfillmentHarness : IAsyncDisposable
{
    private readonly string databasePath;
    private readonly IHost processingHost;
    private readonly IHost projectionHost;
    private readonly FulfillmentApiFactory factory;

    private FulfillmentHarness(
        string databasePath,
        IHost processingHost,
        IHost projectionHost,
        FulfillmentApiFactory factory,
        HttpClient client)
    {
        this.databasePath = databasePath;
        this.processingHost = processingHost;
        this.projectionHost = projectionHost;
        this.factory = factory;
        Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<FulfillmentHarness> CreateAsync(IFulfillmentBrokerFixture brokerFixture, CancellationToken cancellationToken = default)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"layerzero-fulfillment-e2e-{Guid.NewGuid():N}.db");
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionStrings:Fulfillment"] = $"Data Source={databasePath}",
            ["Messaging:ApplicationName"] = $"fulfillment-e2e-{Guid.NewGuid():N}",
        };

        brokerFixture.ApplyConfiguration(settings);

        var processingHost = CreateWorkerHost(settings, static (services, configuration) => ProcessingHost.ConfigureServices(services, configuration));
        var projectionHost = CreateWorkerHost(settings, static (services, configuration) => ProjectionHost.ConfigureServices(services, configuration));

        await FulfillmentProvisioning.InitializeStoreAndProvisionAsync(processingHost.Services, cancellationToken).ConfigureAwait(false);
        await FulfillmentProvisioning.InitializeStoreAndProvisionAsync(projectionHost.Services, cancellationToken).ConfigureAwait(false);

        await processingHost.StartAsync(cancellationToken).ConfigureAwait(false);
        await projectionHost.StartAsync(cancellationToken).ConfigureAwait(false);

        var factory = new FulfillmentApiFactory(settings, databasePath);
        var client = factory.CreateClient();

        return new FulfillmentHarness(databasePath, processingHost, projectionHost, factory, client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await projectionHost.StopAsync().ConfigureAwait(false);
        await processingHost.StopAsync().ConfigureAwait(false);
        await DisposeAsync(projectionHost).ConfigureAwait(false);
        await DisposeAsync(processingHost).ConfigureAwait(false);
        await DisposeAsync(factory).ConfigureAwait(false);

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    public async Task<FulfillmentOrderRun> PlaceOrderAsync(OrderScenario scenario, string? traceParent = null, CancellationToken cancellationToken = default)
    {
        var effectiveTraceParent = string.IsNullOrWhiteSpace(traceParent)
            ? CreateTraceParent()
            : traceParent;

        var request = new PlaceOrderApi.Request(
            "customer@example.com",
            [new OrderItem("LZ-ASYNC", 1)],
            new ShippingAddress("LayerZero Customer", "1 Async Avenue", "Riga", "LV", "LV-1010"),
            scenario);

        using var message = new HttpRequestMessage(HttpMethod.Post, OrderRoutes.Collection)
        {
            Content = JsonContent.Create(request),
        };

        message.Headers.TryAddWithoutValidation("traceparent", effectiveTraceParent);

        var response = await Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var accepted = await response.Content.ReadFromJsonAsync<PlaceOrderApi.Accepted>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Order placement response was empty.");
        return new FulfillmentOrderRun(accepted.OrderId, effectiveTraceParent);
    }

    public async Task CancelOrderAsync(Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostAsJsonAsync(
            OrderRoutes.Cancel.Replace("{id:guid}", orderId.ToString()),
            new CancelOrderApi.Body(reason),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<OrderDetails> WaitForOrderAsync(FulfillmentOrderRun run, Func<OrderDetails, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var order = await Client.GetFromJsonAsync<OrderDetails>($"/orders/{run.OrderId}", cancellationToken).ConfigureAwait(false);
            if (order is not null && predicate(order))
            {
                return order;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for order '{run.OrderId}' to reach the expected state.");
    }

    public async Task<IReadOnlyList<OrderTimelineEntry>> WaitForTimelineAsync(FulfillmentOrderRun run, Func<IReadOnlyList<OrderTimelineEntry>, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timeline = await Client.GetFromJsonAsync<IReadOnlyList<OrderTimelineEntry>>($"/orders/{run.OrderId}/timeline", cancellationToken).ConfigureAwait(false) ?? [];
            if (predicate(timeline))
            {
                return timeline;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for timeline updates for order '{run.OrderId}'.");
    }

    public async Task<IReadOnlyList<DeadLetterRecord>> WaitForDeadLettersAsync(FulfillmentOrderRun run, Func<IReadOnlyList<DeadLetterRecord>, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var records = await Client.GetFromJsonAsync<IReadOnlyList<DeadLetterRecord>>(OrderRoutes.DeadLetters, cancellationToken).ConfigureAwait(false) ?? [];
            var matchingRecords = records.Where(record => run.MatchesTraceParent(record.TraceParent)).ToArray();
            if (predicate(matchingRecords))
            {
                return matchingRecords;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for dead-letter records for order '{run.OrderId}'.");
    }

    private static string CreateTraceParent()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        return $"00-{traceId}-{spanId}-01";
    }

    private static IHost CreateWorkerHost(
        IReadOnlyDictionary<string, string?> settings,
        Action<IServiceCollection, IConfiguration> configure)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        configure(builder.Services, builder.Configuration);
        return builder.Build();
    }

    private static async ValueTask DisposeAsync(object instance)
    {
        switch (instance)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    private sealed class FulfillmentApiFactory(IReadOnlyDictionary<string, string?> settings, string databasePath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            foreach (var setting in settings)
            {
                if (!string.IsNullOrWhiteSpace(setting.Value))
                {
                    builder.UseSetting(setting.Key, setting.Value);
                }
            }

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(settings);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
