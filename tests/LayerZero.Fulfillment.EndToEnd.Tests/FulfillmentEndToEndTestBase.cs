using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Messaging.IntegrationTesting;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

public abstract class FulfillmentEndToEndTestBase
{
    public static bool SkipWhenCloudEnvironmentUnavailable => false;

    protected abstract FulfillmentHarness Harness { get; }

    [OptionalCloudEnvironmentFact]
    public async Task Happy_path_completes_the_order()
    {
        var run = await Harness.PlaceOrderAsync(new OrderScenario(), cancellationToken: TestContext.Current.CancellationToken);
        var order = await Harness.WaitForOrderAsync(
            run,
            static current => current.Status == OrderStatuses.Completed,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.True(order.InventoryReserved);
        Assert.True(order.PaymentAuthorized);
        Assert.NotNull(order.TrackingNumber);
    }

    [OptionalCloudEnvironmentFact]
    public async Task Inventory_rejection_stops_the_workflow()
    {
        var run = await Harness.PlaceOrderAsync(
            new OrderScenario(ForceInventoryFailure: true),
            cancellationToken: TestContext.Current.CancellationToken);

        var order = await Harness.WaitForOrderAsync(
            run,
            static current => current.Status == OrderStatuses.InventoryRejected,
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);

        Assert.False(order.InventoryReserved);
        Assert.False(order.PaymentAuthorized);
        Assert.Null(order.TrackingNumber);
    }

    [OptionalCloudEnvironmentFact]
    public async Task Payment_timeout_retries_and_then_completes()
    {
        var run = await Harness.PlaceOrderAsync(
            new OrderScenario(ForcePaymentTimeoutOnce: true),
            cancellationToken: TestContext.Current.CancellationToken);

        var order = await Harness.WaitForOrderAsync(
            run,
            static current => current.Status == OrderStatuses.Completed,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);
        var timeline = await Harness.WaitForTimelineAsync(
            run,
            static current => current.Count(entry => entry.Step == "payment.authorization") >= 2,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatuses.Completed, order.Status);
        Assert.True(timeline.Count(entry => entry.Step == "payment.authorization") >= 2);
    }

    [OptionalCloudEnvironmentFact]
    public async Task Projection_poison_messages_are_dead_lettered()
    {
        var run = await Harness.PlaceOrderAsync(
            new OrderScenario(ForceProjectionPoisonMessage: true),
            cancellationToken: TestContext.Current.CancellationToken);

        _ = await Harness.WaitForOrderAsync(
            run,
            static current => current.Status == OrderStatuses.Completed,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        var deadLetters = await Harness.WaitForDeadLettersAsync(
            run,
            static records => records.Any(record => record.HandlerIdentity.Contains("OrderPlacedAnalyticsProjection", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Contains(deadLetters, record => record.HandlerIdentity.Contains("OrderPlacedAnalyticsProjection", StringComparison.Ordinal));
    }

    [OptionalCloudEnvironmentFact]
    public async Task Duplicate_shipment_requests_are_suppressed()
    {
        var run = await Harness.PlaceOrderAsync(
            new OrderScenario(ForceDuplicateShipment: true),
            cancellationToken: TestContext.Current.CancellationToken);

        var timeline = await Harness.WaitForTimelineAsync(
            run,
            static current => current.Any(entry => entry.Step == "shipment.duplicate.suppressed"),
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, timeline.Count(entry => entry.Step == "shipment.preparation"));
        Assert.Equal(1, timeline.Count(entry => entry.Step == "shipment.duplicate.suppressed"));
    }

    [OptionalCloudEnvironmentFact]
    public async Task Cancel_during_processing_prevents_completion()
    {
        var run = await Harness.PlaceOrderAsync(
            new OrderScenario(ForcePaymentTimeoutOnce: true),
            cancellationToken: TestContext.Current.CancellationToken);
        await Harness.CancelOrderAsync(run.OrderId, "Customer changed their mind.", TestContext.Current.CancellationToken);

        var order = await Harness.WaitForOrderAsync(
            run,
            static current => current.Status == OrderStatuses.Cancelled,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatuses.Cancelled, order.Status);
    }

    [OptionalCloudEnvironmentFact]
    public async Task Http_trace_ids_flow_into_async_message_timeline_entries()
    {
        const string traceParent = "00-11111111111111111111111111111111-2222222222222222-01";
        var run = await Harness.PlaceOrderAsync(
            new OrderScenario(),
            traceParent,
            TestContext.Current.CancellationToken);

        var timeline = await Harness.WaitForTimelineAsync(
            run,
            static current => current.Any(entry => !string.IsNullOrWhiteSpace(entry.TraceParent)),
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Contains(
            timeline.Where(entry => !string.IsNullOrWhiteSpace(entry.TraceParent)),
            entry => run.MatchesTraceParent(entry.TraceParent));
    }
}

public abstract class PerTestFulfillmentEndToEndTestBase : FulfillmentEndToEndTestBase, IAsyncLifetime
{
    private FulfillmentHarness? harness;

    protected override FulfillmentHarness Harness => harness
        ?? throw new InvalidOperationException("The fulfillment harness has not been initialized.");

    protected abstract Task<FulfillmentHarness> CreateHarnessAsync();

    public virtual async ValueTask InitializeAsync()
    {
        harness = await CreateHarnessAsync().ConfigureAwait(false);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (harness is null)
        {
            return;
        }

        await harness.DisposeAsync().ConfigureAwait(false);
        harness = null;
    }
}
