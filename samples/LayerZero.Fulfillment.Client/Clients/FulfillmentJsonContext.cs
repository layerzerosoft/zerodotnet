using System.Text.Json.Serialization;
using LayerZero.Fulfillment.Contracts.Orders;

namespace LayerZero.Fulfillment.Client.Sample.Clients;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PlaceOrderApi.Request))]
[JsonSerializable(typeof(PlaceOrderApi.Accepted))]
[JsonSerializable(typeof(OrderDetails))]
[JsonSerializable(typeof(IReadOnlyList<OrderTimelineEntry>))]
[JsonSerializable(typeof(IReadOnlyList<DeadLetterRecord>))]
internal sealed partial class FulfillmentJsonContext : JsonSerializerContext;
