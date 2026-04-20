using LayerZero.Client;
using LayerZero.Fulfillment.Client.Sample.Clients;
using LayerZero.Fulfillment.Contracts.Orders;
using Microsoft.Extensions.DependencyInjection;

var baseAddress = new Uri(args.FirstOrDefault() ?? "http://localhost:5381", UriKind.Absolute);

var services = new ServiceCollection();
services.AddLayerZeroClient<FulfillmentClient>(client =>
{
    client.BaseAddress = baseAddress;
});

using var provider = services.BuildServiceProvider();
var api = provider.GetRequiredService<FulfillmentClient>();

var placed = await api.PlaceOrderAsync(new PlaceOrderApi.Request(
    "customer@example.com",
    [new OrderItem("LZ-CORE", 2)],
    new ShippingAddress("LayerZero Customer", "1 Async Avenue", "Riga", "LV", "LV-1010"),
    new OrderScenario(ForcePaymentTimeoutOnce: true)));

if (placed.IsFailure)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, placed.Errors.Select(static error => error.Message)));
    return;
}

Console.WriteLine($"Accepted order {placed.Value.OrderId}");
var order = await api.GetOrderForResponseAsync(placed.Value.OrderId);
Console.WriteLine($"Initial status: {(int)order.StatusCode}");
