using LayerZero.Fulfillment.Contracts.Orders;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

public abstract class FulfillmentEndToEndTestBase
{
    protected abstract Task<FulfillmentHarness> CreateHarnessAsync();

    [Fact]
    public async Task Happy_path_completes_the_order()
    {
        await using var harness = await CreateHarnessAsync();

        var orderId = await harness.PlaceOrderAsync(new OrderScenario(), cancellationToken: TestContext.Current.CancellationToken);
        var order = await harness.WaitForOrderAsync(
            orderId,
            static current => current.Status == OrderStatuses.Completed,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.True(order.InventoryReserved);
        Assert.True(order.PaymentAuthorized);
        Assert.NotNull(order.TrackingNumber);
    }

    [Fact]
    public async Task Inventory_rejection_stops_the_workflow()
    {
        await using var harness = await CreateHarnessAsync();

        var orderId = await harness.PlaceOrderAsync(
            new OrderScenario(ForceInventoryFailure: true),
            cancellationToken: TestContext.Current.CancellationToken);

        var order = await harness.WaitForOrderAsync(
            orderId,
            static current => current.Status == OrderStatuses.InventoryRejected,
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken);

        Assert.False(order.InventoryReserved);
        Assert.False(order.PaymentAuthorized);
        Assert.Null(order.TrackingNumber);
    }

    [Fact]
    public async Task Payment_timeout_retries_and_then_completes()
    {
        await using var harness = await CreateHarnessAsync();

        var orderId = await harness.PlaceOrderAsync(
            new OrderScenario(ForcePaymentTimeoutOnce: true),
            cancellationToken: TestContext.Current.CancellationToken);

        var order = await harness.WaitForOrderAsync(
            orderId,
            static current => current.Status == OrderStatuses.Completed,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);
        var timeline = await harness.WaitForTimelineAsync(
            orderId,
            static current => current.Count(entry => entry.Step == "payment.authorization") >= 2,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatuses.Completed, order.Status);
        Assert.True(timeline.Count(entry => entry.Step == "payment.authorization") >= 2);
    }

    [Fact]
    public async Task Projection_poison_messages_are_dead_lettered()
    {
        await using var harness = await CreateHarnessAsync();

        var orderId = await harness.PlaceOrderAsync(
            new OrderScenario(ForceProjectionPoisonMessage: true),
            cancellationToken: TestContext.Current.CancellationToken);

        _ = await harness.WaitForOrderAsync(
            orderId,
            static current => current.Status == OrderStatuses.Completed,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        var deadLetters = await harness.WaitForDeadLettersAsync(
            static records => records.Any(record => record.HandlerIdentity.Contains("OrderPlacedAnalyticsProjection", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Contains(deadLetters, record => record.HandlerIdentity.Contains("OrderPlacedAnalyticsProjection", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Duplicate_shipment_requests_are_suppressed()
    {
        await using var harness = await CreateHarnessAsync();

        var orderId = await harness.PlaceOrderAsync(
            new OrderScenario(ForceDuplicateShipment: true),
            cancellationToken: TestContext.Current.CancellationToken);

        var timeline = await harness.WaitForTimelineAsync(
            orderId,
            static current => current.Any(entry => entry.Step == "shipment.duplicate.suppressed"),
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, timeline.Count(entry => entry.Step == "shipment.preparation"));
        Assert.Equal(1, timeline.Count(entry => entry.Step == "shipment.duplicate.suppressed"));
    }

    [Fact]
    public async Task Cancel_during_processing_prevents_completion()
    {
        await using var harness = await CreateHarnessAsync();

        var orderId = await harness.PlaceOrderAsync(
            new OrderScenario(ForcePaymentTimeoutOnce: true),
            cancellationToken: TestContext.Current.CancellationToken);
        await harness.CancelOrderAsync(orderId, "Customer changed their mind.", TestContext.Current.CancellationToken);

        var order = await harness.WaitForOrderAsync(
            orderId,
            static current => current.Status == OrderStatuses.Cancelled,
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatuses.Cancelled, order.Status);
    }

    [Fact]
    public async Task Http_trace_ids_flow_into_async_message_timeline_entries()
    {
        await using var harness = await CreateHarnessAsync();

        const string traceParent = "00-11111111111111111111111111111111-2222222222222222-01";
        var orderId = await harness.PlaceOrderAsync(
            new OrderScenario(),
            traceParent,
            TestContext.Current.CancellationToken);

        var timeline = await harness.WaitForTimelineAsync(
            orderId,
            static current => current.Any(entry => !string.IsNullOrWhiteSpace(entry.TraceParent)),
            TimeSpan.FromSeconds(45),
            TestContext.Current.CancellationToken);

        Assert.Contains(timeline.Where(entry => !string.IsNullOrWhiteSpace(entry.TraceParent)), entry => entry.TraceParent!.Contains("11111111111111111111111111111111", StringComparison.Ordinal));
    }
}
