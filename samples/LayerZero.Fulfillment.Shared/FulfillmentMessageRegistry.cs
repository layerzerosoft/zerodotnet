using System.Text.Json;
using System.Text.Json.Serialization;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Messaging;

namespace LayerZero.Fulfillment.Shared;

public sealed class FulfillmentMessageRegistry : IMessageRegistry
{
    private static readonly MessageDescriptor[] MessagesValue =
    [
        CreateDescriptor<PlaceOrder>(MessageKind.Command, static message => message.OrderId.ToString("N")),
        CreateDescriptor<CancelOrder>(MessageKind.Command, static message => message.OrderId.ToString("N")),
        CreateDescriptor<ReserveInventory>(MessageKind.Command, static message => message.OrderId.ToString("N")),
        CreateDescriptor<AuthorizePayment>(MessageKind.Command, static message => message.OrderId.ToString("N"), requiresIdempotency: true),
        CreateDescriptor<PrepareShipment>(MessageKind.Command, static message => message.OrderId.ToString("N"), requiresIdempotency: true),
        CreateDescriptor<DispatchShipment>(MessageKind.Command, static message => message.OrderId.ToString("N")),
        CreateDescriptor<OrderPlaced>(MessageKind.Event, static message => message.OrderId.ToString("N")),
        CreateDescriptor<InventoryReserved>(MessageKind.Event, static message => message.OrderId.ToString("N")),
        CreateDescriptor<InventoryRejected>(MessageKind.Event, static message => message.OrderId.ToString("N")),
        CreateDescriptor<PaymentAuthorized>(MessageKind.Event, static message => message.OrderId.ToString("N")),
        CreateDescriptor<PaymentDeclined>(MessageKind.Event, static message => message.OrderId.ToString("N")),
        CreateDescriptor<ShipmentPrepared>(MessageKind.Event, static message => message.OrderId.ToString("N")),
        CreateDescriptor<OrderCompleted>(MessageKind.Event, static message => message.OrderId.ToString("N")),
        CreateDescriptor<OrderCancelled>(MessageKind.Event, static message => message.OrderId.ToString("N")),
    ];

    private static readonly IReadOnlyDictionary<Type, MessageDescriptor> ByType = MessagesValue.ToDictionary(static descriptor => descriptor.MessageType);
    private static readonly IReadOnlyDictionary<string, MessageDescriptor> ByName = MessagesValue.ToDictionary(static descriptor => descriptor.Name, StringComparer.Ordinal);

    public IReadOnlyList<MessageDescriptor> Messages => MessagesValue;

    public bool TryGetDescriptor(Type messageType, out MessageDescriptor descriptor)
    {
        return ByType.TryGetValue(messageType, out descriptor!);
    }

    public bool TryGetDescriptor(string messageName, out MessageDescriptor descriptor)
    {
        return ByName.TryGetValue(messageName, out descriptor!);
    }

    private static MessageDescriptor CreateDescriptor<TMessage>(
        MessageKind kind,
        Func<TMessage, string?> affinityAccessor,
        bool requiresIdempotency = false)
    {
        return new MessageDescriptor(
            MessageNames.For<TMessage>(),
            typeof(TMessage),
            kind,
            FulfillmentMessageJsonContext.Default.GetTypeInfo(typeof(TMessage))!,
            MessageTopologyNames.Entity(kind, MessageNames.For<TMessage>()),
            requiresIdempotency,
            "OrderId",
            message => affinityAccessor((TMessage)message));
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PlaceOrder))]
[JsonSerializable(typeof(CancelOrder))]
[JsonSerializable(typeof(ReserveInventory))]
[JsonSerializable(typeof(AuthorizePayment))]
[JsonSerializable(typeof(PrepareShipment))]
[JsonSerializable(typeof(DispatchShipment))]
[JsonSerializable(typeof(OrderPlaced))]
[JsonSerializable(typeof(InventoryReserved))]
[JsonSerializable(typeof(InventoryRejected))]
[JsonSerializable(typeof(PaymentAuthorized))]
[JsonSerializable(typeof(PaymentDeclined))]
[JsonSerializable(typeof(ShipmentPrepared))]
[JsonSerializable(typeof(OrderCompleted))]
[JsonSerializable(typeof(OrderCancelled))]
internal sealed partial class FulfillmentMessageJsonContext : JsonSerializerContext
{
}
