using LayerZero.Core;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;

namespace LayerZero.Fulfillment.Processing.Workflows;

public sealed class PlaceOrderHandler(FulfillmentStore store, IEventPublisher publisher) : ICommandHandler<PlaceOrder>
{
    public async ValueTask<Result> HandleAsync(PlaceOrder command, CancellationToken cancellationToken = default)
    {
        await store.UpdateOrderStatusAsync(command.OrderId, OrderStatuses.Accepted, cancellationToken: cancellationToken);
        await store.AppendTimelineAsync(command.OrderId, "order.placed", "The processing worker accepted the order command.", "processing", GetType().FullName, cancellationToken);
        return await publisher.PublishAsync(new OrderPlaced(command.OrderId, command.CustomerEmail, command.Items, command.ShippingAddress, command.Scenario), cancellationToken);
    }
}

public sealed class CancelOrderHandler(FulfillmentStore store, IEventPublisher publisher) : ICommandHandler<CancelOrder>
{
    public async ValueTask<Result> HandleAsync(CancelOrder command, CancellationToken cancellationToken = default)
    {
        await store.UpdateOrderStatusAsync(command.OrderId, OrderStatuses.CancelRequested, cancelRequested: true, cancellationToken: cancellationToken);
        await store.AppendTimelineAsync(command.OrderId, "order.cancel.requested", command.Reason, "processing", GetType().FullName, cancellationToken);
        return await publisher.PublishAsync(new OrderCancelled(command.OrderId, command.Reason), cancellationToken);
    }
}

public sealed class OrderPlacedWorkflow(ICommandSender sender) : IEventHandler<OrderPlaced>
{
    public async ValueTask<Result> HandleAsync(OrderPlaced message, CancellationToken cancellationToken = default)
    {
        return await sender.SendAsync(new ReserveInventory(message.OrderId, message.Items, message.Scenario), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ReserveInventoryHandler(FulfillmentStore store, IEventPublisher publisher) : ICommandHandler<ReserveInventory>
{
    public async ValueTask<Result> HandleAsync(ReserveInventory command, CancellationToken cancellationToken = default)
    {
        await store.AppendTimelineAsync(command.OrderId, "inventory.check", "Inventory reservation started.", "processing", GetType().FullName, cancellationToken);
        return command.Scenario.ForceInventoryFailure
            ? await publisher.PublishAsync(new InventoryRejected(command.OrderId, "Inventory allocation failed for one or more SKUs."), cancellationToken).ConfigureAwait(false)
            : await publisher.PublishAsync(new InventoryReserved(command.OrderId), cancellationToken).ConfigureAwait(false);
    }
}

[IdempotentHandler]
public sealed class AuthorizePaymentHandler(FulfillmentStore store, IEventPublisher publisher) : ICommandHandler<AuthorizePayment>
{
    public async ValueTask<Result> HandleAsync(AuthorizePayment command, CancellationToken cancellationToken = default)
    {
        await store.AppendTimelineAsync(command.OrderId, "payment.authorization", "Payment authorization started.", "processing", GetType().FullName, cancellationToken);

        if (command.Scenario.ForcePaymentTimeoutOnce
            && await store.TryConsumePaymentTimeoutAsync(command.OrderId, cancellationToken).ConfigureAwait(false))
        {
            throw new TimeoutException("Simulated payment gateway timeout.");
        }

        if (command.Scenario.ForcePaymentDecline)
        {
            return await publisher.PublishAsync(new PaymentDeclined(command.OrderId, "Payment authorization was declined."), cancellationToken).ConfigureAwait(false);
        }

        if (!await store.TryRecordSideEffectAsync($"payment:{command.OrderId:N}", command.CustomerEmail, cancellationToken).ConfigureAwait(false))
        {
            await store.AppendTimelineAsync(command.OrderId, "payment.duplicate.suppressed", "Payment authorization duplicate was suppressed.", "processing", GetType().FullName, cancellationToken);
            return Result.Success();
        }

        return await publisher.PublishAsync(new PaymentAuthorized(command.OrderId), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class InventoryReservedWorkflow(FulfillmentStore store, ICommandSender sender) : IEventHandler<InventoryReserved>
{
    public async ValueTask<Result> HandleAsync(InventoryReserved message, CancellationToken cancellationToken = default)
    {
        await store.UpdateOrderStatusAsync(message.OrderId, OrderStatuses.InventoryReserved, inventoryReserved: true, cancellationToken: cancellationToken);
        await store.AppendTimelineAsync(message.OrderId, "inventory.reserved", "Inventory reservation completed.", "processing", GetType().FullName, cancellationToken);

        var order = await store.GetOrderAsync(message.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null || order.CancelRequested)
        {
            return Result.Success();
        }

        return await sender.SendAsync(
            new AuthorizePayment(message.OrderId, order.CustomerEmail, order.Items, order.Scenario),
            cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PaymentAuthorizedWorkflow(FulfillmentStore store, ICommandSender sender) : IEventHandler<PaymentAuthorized>
{
    public async ValueTask<Result> HandleAsync(PaymentAuthorized message, CancellationToken cancellationToken = default)
    {
        await store.UpdateOrderStatusAsync(message.OrderId, OrderStatuses.PaymentAuthorized, paymentAuthorized: true, cancellationToken: cancellationToken);
        await store.AppendTimelineAsync(message.OrderId, "payment.authorized", "Payment authorization completed.", "processing", GetType().FullName, cancellationToken);
        return await ShipmentReadiness.TryPrepareShipmentAsync(store, sender, message.OrderId, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class InventoryRejectedWorkflow(FulfillmentStore store) : IEventHandler<InventoryRejected>
{
    public async ValueTask<Result> HandleAsync(InventoryRejected message, CancellationToken cancellationToken = default)
    {
        await store.UpdateOrderStatusAsync(message.OrderId, OrderStatuses.InventoryRejected, cancellationToken: cancellationToken);
        await store.AppendTimelineAsync(message.OrderId, "inventory.rejected", message.Reason, "processing", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

public sealed class PaymentDeclinedWorkflow(FulfillmentStore store) : IEventHandler<PaymentDeclined>
{
    public async ValueTask<Result> HandleAsync(PaymentDeclined message, CancellationToken cancellationToken = default)
    {
        await store.UpdateOrderStatusAsync(message.OrderId, OrderStatuses.PaymentDeclined, cancellationToken: cancellationToken);
        await store.AppendTimelineAsync(message.OrderId, "payment.declined", message.Reason, "processing", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

[IdempotentHandler]
public sealed class PrepareShipmentHandler(FulfillmentStore store, IEventPublisher publisher) : ICommandHandler<PrepareShipment>
{
    public async ValueTask<Result> HandleAsync(PrepareShipment command, CancellationToken cancellationToken = default)
    {
        var trackingNumber = $"TRK-{command.OrderId:N}"[..16];

        if (!await store.TryRecordSideEffectAsync($"shipment:{command.OrderId:N}", trackingNumber, cancellationToken).ConfigureAwait(false))
        {
            await store.AppendTimelineAsync(command.OrderId, "shipment.duplicate.suppressed", "Shipment preparation duplicate was suppressed.", "processing", GetType().FullName, cancellationToken);
            return Result.Success();
        }

        await store.AppendTimelineAsync(command.OrderId, "shipment.preparation", $"Shipment prepared with tracking number {trackingNumber}.", "processing", GetType().FullName, cancellationToken);
        return await publisher.PublishAsync(new ShipmentPrepared(command.OrderId, trackingNumber, command.Scenario), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ShipmentPreparedWorkflow(FulfillmentStore store, ICommandSender sender) : IEventHandler<ShipmentPrepared>
{
    public async ValueTask<Result> HandleAsync(ShipmentPrepared message, CancellationToken cancellationToken = default)
    {
        await store.UpdateOrderStatusAsync(message.OrderId, OrderStatuses.ShipmentPrepared, trackingNumber: message.TrackingNumber, cancellationToken: cancellationToken);
        await store.AppendTimelineAsync(message.OrderId, "shipment.prepared", "Shipment is ready to dispatch.", "processing", GetType().FullName, cancellationToken);
        return await sender.SendAsync(new DispatchShipment(message.OrderId, message.TrackingNumber), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DispatchShipmentHandler(IEventPublisher publisher) : ICommandHandler<DispatchShipment>
{
    public async ValueTask<Result> HandleAsync(DispatchShipment command, CancellationToken cancellationToken = default)
    {
        return await publisher.PublishAsync(new OrderCompleted(command.OrderId, command.TrackingNumber), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class OrderCompletedWorkflow(FulfillmentStore store) : IEventHandler<OrderCompleted>
{
    public async ValueTask<Result> HandleAsync(OrderCompleted message, CancellationToken cancellationToken = default)
    {
        await store.UpdateOrderStatusAsync(message.OrderId, OrderStatuses.Completed, trackingNumber: message.TrackingNumber, cancellationToken: cancellationToken);
        await store.AppendTimelineAsync(message.OrderId, "order.completed", "The order workflow finished successfully.", "processing", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

public sealed class OrderCancelledWorkflow(FulfillmentStore store) : IEventHandler<OrderCancelled>
{
    public async ValueTask<Result> HandleAsync(OrderCancelled message, CancellationToken cancellationToken = default)
    {
        await store.UpdateOrderStatusAsync(message.OrderId, OrderStatuses.Cancelled, cancelRequested: true, cancellationToken: cancellationToken);
        await store.AppendTimelineAsync(message.OrderId, "order.cancelled", message.Reason, "processing", GetType().FullName, cancellationToken);
        return Result.Success();
    }
}

internal static class ShipmentReadiness
{
    public static async ValueTask<Result> TryPrepareShipmentAsync(
        FulfillmentStore store,
        ICommandSender sender,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await store.GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null || !order.InventoryReserved || !order.PaymentAuthorized || order.CancelRequested || order.TrackingNumber is not null)
        {
            return Result.Success();
        }

        var prepareShipment = new PrepareShipment(orderId, order.ShippingAddress, order.Scenario);
        var result = await sender.SendAsync(prepareShipment, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure || !order.Scenario.ForceDuplicateShipment)
        {
            return result;
        }

        var duplicateResult = await sender.SendAsync(prepareShipment, cancellationToken).ConfigureAwait(false);
        return duplicateResult.IsFailure ? duplicateResult : result;
    }
}
