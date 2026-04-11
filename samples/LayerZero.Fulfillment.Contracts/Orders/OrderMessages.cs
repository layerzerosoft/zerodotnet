using LayerZero.Core;
using LayerZero.Messaging;

namespace LayerZero.Fulfillment.Contracts.Orders;

[AffinityKey(nameof(OrderId))]
public sealed record PlaceOrder(
    Guid OrderId,
    string CustomerEmail,
    IReadOnlyList<OrderItem> Items,
    ShippingAddress ShippingAddress,
    OrderScenario Scenario) : ICommand;

[AffinityKey(nameof(OrderId))]
public sealed record CancelOrder(Guid OrderId, string Reason) : ICommand;

[AffinityKey(nameof(OrderId))]
public sealed record ReserveInventory(Guid OrderId, IReadOnlyList<OrderItem> Items, OrderScenario Scenario) : ICommand;

[AffinityKey(nameof(OrderId))]
[IdempotentMessage]
public sealed record AuthorizePayment(Guid OrderId, string CustomerEmail, IReadOnlyList<OrderItem> Items, OrderScenario Scenario) : ICommand;

[AffinityKey(nameof(OrderId))]
[IdempotentMessage]
public sealed record PrepareShipment(Guid OrderId, ShippingAddress ShippingAddress, OrderScenario Scenario) : ICommand;

[AffinityKey(nameof(OrderId))]
public sealed record DispatchShipment(Guid OrderId, string TrackingNumber) : ICommand;

[AffinityKey(nameof(OrderId))]
public sealed record OrderPlaced(
    Guid OrderId,
    string CustomerEmail,
    IReadOnlyList<OrderItem> Items,
    ShippingAddress ShippingAddress,
    OrderScenario Scenario) : IEvent;

[AffinityKey(nameof(OrderId))]
public sealed record InventoryReserved(Guid OrderId) : IEvent;

[AffinityKey(nameof(OrderId))]
public sealed record InventoryRejected(Guid OrderId, string Reason) : IEvent;

[AffinityKey(nameof(OrderId))]
public sealed record PaymentAuthorized(Guid OrderId) : IEvent;

[AffinityKey(nameof(OrderId))]
public sealed record PaymentDeclined(Guid OrderId, string Reason) : IEvent;

[AffinityKey(nameof(OrderId))]
public sealed record ShipmentPrepared(Guid OrderId, string TrackingNumber, OrderScenario Scenario) : IEvent;

[AffinityKey(nameof(OrderId))]
public sealed record OrderCompleted(Guid OrderId, string TrackingNumber) : IEvent;

[AffinityKey(nameof(OrderId))]
public sealed record OrderCancelled(Guid OrderId, string Reason) : IEvent;
