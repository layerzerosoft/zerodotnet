using LayerZero.Core;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.Shared;

namespace LayerZero.Fulfillment.Projections.Handlers;

public sealed class OrderPlacedAuditProjection(FulfillmentStore store) : IEventHandler<OrderPlaced>
{
    public async ValueTask<Result> HandleAsync(OrderPlaced message, CancellationToken cancellationToken = default)
    {
        await store.AppendTimelineAsync(message.OrderId, "projection.audit", "Audit projection captured OrderPlaced.", "projections", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

public sealed class OrderPlacedNotificationProjection(FulfillmentStore store) : IEventHandler<OrderPlaced>
{
    public async ValueTask<Result> HandleAsync(OrderPlaced message, CancellationToken cancellationToken = default)
    {
        await store.AppendTimelineAsync(message.OrderId, "projection.notification", $"Customer notification queued for {message.CustomerEmail}.", "projections", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

public sealed class OrderPlacedAnalyticsProjection(FulfillmentStore store) : IEventHandler<OrderPlaced>
{
    public async ValueTask<Result> HandleAsync(OrderPlaced message, CancellationToken cancellationToken = default)
    {
        if (message.Scenario.ForceProjectionPoisonMessage)
        {
            throw new FormatException("The analytics projection intentionally failed to demonstrate dead-letter handling.");
        }

        await store.AppendTimelineAsync(message.OrderId, "projection.analytics", "Analytics projection recorded order placement.", "projections", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

public sealed class InventoryReservedProjection(FulfillmentStore store) : IEventHandler<InventoryReserved>
{
    public async ValueTask<Result> HandleAsync(InventoryReserved message, CancellationToken cancellationToken = default)
    {
        await store.AppendTimelineAsync(message.OrderId, "projection.inventory", "Projection observed inventory reservation.", "projections", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

public sealed class PaymentAuthorizedProjection(FulfillmentStore store) : IEventHandler<PaymentAuthorized>
{
    public async ValueTask<Result> HandleAsync(PaymentAuthorized message, CancellationToken cancellationToken = default)
    {
        await store.AppendTimelineAsync(message.OrderId, "projection.payment", "Projection observed payment authorization.", "projections", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

public sealed class OrderCompletedProjection(FulfillmentStore store) : IEventHandler<OrderCompleted>
{
    public async ValueTask<Result> HandleAsync(OrderCompleted message, CancellationToken cancellationToken = default)
    {
        await store.AppendTimelineAsync(message.OrderId, "projection.completed", $"Projection observed completion with tracking {message.TrackingNumber}.", "projections", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

public sealed class OrderCancelledProjection(FulfillmentStore store) : IEventHandler<OrderCancelled>
{
    public async ValueTask<Result> HandleAsync(OrderCancelled message, CancellationToken cancellationToken = default)
    {
        await store.AppendTimelineAsync(message.OrderId, "projection.cancelled", message.Reason, "projections", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}
