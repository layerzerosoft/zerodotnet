using System.Net.Http.Json;
using LayerZero.Fulfillment.Api.Features.Orders.Place;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.Processing;
using LayerZero.Fulfillment.Projections;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

public sealed class FulfillmentHarness : IAsyncDisposable
{
    private readonly string databasePath;
    private readonly Dictionary<string, string?> settings;
    private readonly IHost processingHost;
    private readonly IHost projectionHost;
    private readonly FulfillmentApiFactory factory;

    private FulfillmentHarness(
        string databasePath,
        Dictionary<string, string?> settings,
        IHost processingHost,
        IHost projectionHost,
        FulfillmentApiFactory factory,
        HttpClient client)
    {
        this.databasePath = databasePath;
        this.settings = settings;
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

        await InitializeStoreAndProvisionAsync(processingHost, cancellationToken).ConfigureAwait(false);
        await InitializeStoreAndProvisionAsync(projectionHost, cancellationToken).ConfigureAwait(false);

        await processingHost.StartAsync(cancellationToken).ConfigureAwait(false);
        await projectionHost.StartAsync(cancellationToken).ConfigureAwait(false);

        var factory = new FulfillmentApiFactory(settings, databasePath);
        var client = factory.CreateClient();

        return new FulfillmentHarness(databasePath, settings, processingHost, projectionHost, factory, client);
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

    public async Task<Guid> PlaceOrderAsync(OrderScenario scenario, string? traceParent = null, CancellationToken cancellationToken = default)
    {
        var request = new PlaceOrderApi.Request(
            "customer@example.com",
            [new OrderItem("LZ-ASYNC", 1)],
            new ShippingAddress("LayerZero Customer", "1 Async Avenue", "Riga", "LV", "LV-1010"),
            scenario);

        using var message = new HttpRequestMessage(HttpMethod.Post, OrderRoutes.Collection)
        {
            Content = JsonContent.Create(request),
        };

        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            message.Headers.TryAddWithoutValidation("traceparent", traceParent);
        }

        var response = await Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var accepted = await response.Content.ReadFromJsonAsync<PlaceOrderApi.Accepted>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Order placement response was empty.");
        return accepted.OrderId;
    }

    public async Task CancelOrderAsync(Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostAsJsonAsync(
            OrderRoutes.Cancel.Replace("{id:guid}", orderId.ToString()),
            new CancelOrderApi.Body(reason),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<OrderDetails> WaitForOrderAsync(Guid orderId, Func<OrderDetails, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var order = await Client.GetFromJsonAsync<OrderDetails>($"/orders/{orderId}", cancellationToken).ConfigureAwait(false);
            if (order is not null && predicate(order))
            {
                return order;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for order '{orderId}' to reach the expected state.");
    }

    public async Task<IReadOnlyList<OrderTimelineEntry>> WaitForTimelineAsync(Guid orderId, Func<IReadOnlyList<OrderTimelineEntry>, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timeline = await Client.GetFromJsonAsync<IReadOnlyList<OrderTimelineEntry>>($"/orders/{orderId}/timeline", cancellationToken).ConfigureAwait(false) ?? [];
            if (predicate(timeline))
            {
                return timeline;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for timeline updates for order '{orderId}'.");
    }

    public async Task<IReadOnlyList<DeadLetterRecord>> WaitForDeadLettersAsync(Func<IReadOnlyList<DeadLetterRecord>, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var records = await Client.GetFromJsonAsync<IReadOnlyList<DeadLetterRecord>>(OrderRoutes.DeadLetters, cancellationToken).ConfigureAwait(false) ?? [];
            if (predicate(records))
            {
                return records;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for dead-letter records.");
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

    private static async Task InitializeStoreAndProvisionAsync(IHost host, CancellationToken cancellationToken)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FulfillmentStore>().InitializeAsync(cancellationToken).ConfigureAwait(false);
        foreach (var manager in scope.ServiceProvider.GetServices<IMessageTopologyManager>())
        {
            await manager.ProvisionAsync(cancellationToken).ConfigureAwait(false);
        }
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
