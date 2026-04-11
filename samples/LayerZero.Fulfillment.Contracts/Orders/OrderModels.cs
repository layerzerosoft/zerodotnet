namespace LayerZero.Fulfillment.Contracts.Orders;

public sealed record OrderItem(string Sku, int Quantity);

public sealed record ShippingAddress(string Recipient, string Line1, string City, string CountryCode, string PostalCode);

public sealed record OrderScenario(
    bool ForceInventoryFailure = false,
    bool ForcePaymentTimeoutOnce = false,
    bool ForcePaymentDecline = false,
    bool ForceDuplicateShipment = false,
    bool ForceProjectionPoisonMessage = false);

public sealed record OrderDetails(
    Guid Id,
    string CustomerEmail,
    string Status,
    bool InventoryReserved,
    bool PaymentAuthorized,
    bool CancelRequested,
    string? TrackingNumber,
    IReadOnlyList<OrderItem> Items,
    ShippingAddress ShippingAddress,
    OrderScenario Scenario);

public sealed record OrderTimelineEntry(
    long Sequence,
    string Step,
    string Detail,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    string? MessageId,
    string? HandlerIdentity,
    int? Attempt,
    string? TransportName,
    string? EntityName,
    string? CorrelationId,
    string? TraceParent);

public sealed record DeadLetterRecord(
    string MessageId,
    string MessageName,
    string HandlerIdentity,
    string TransportName,
    string EntityName,
    int Attempt,
    string? CorrelationId,
    string? TraceParent,
    string Reason,
    string Errors,
    DateTimeOffset FailedAtUtc,
    bool Requeued);

public static class OrderStatuses
{
    public const string Draft = "draft";
    public const string Accepted = "accepted";
    public const string InventoryReserved = "inventory-reserved";
    public const string InventoryRejected = "inventory-rejected";
    public const string PaymentAuthorized = "payment-authorized";
    public const string PaymentDeclined = "payment-declined";
    public const string ShipmentPrepared = "shipment-prepared";
    public const string Completed = "completed";
    public const string CancelRequested = "cancel-requested";
    public const string Cancelled = "cancelled";
}
