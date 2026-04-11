using System.Net;
using LayerZero.Client;
using LayerZero.Fulfillment.Client.Sample.Clients;
using LayerZero.Fulfillment.Contracts.Orders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Client.Integration.Tests;

public sealed class FulfillmentTypedClientIntegrationTests : IClassFixture<FulfillmentTypedClientFactory>
{
    private readonly FulfillmentTypedClientFactory factory;

    public FulfillmentTypedClientIntegrationTests(FulfillmentTypedClientFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Explicit_typed_client_handles_place_get_and_timeline_flows()
    {
        var client = CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var created = await client.PlaceOrderAsync(
            new PlaceOrderApi.Request(
                "customer@example.com",
                [new OrderItem("LZ-CORE", 2)],
                new ShippingAddress("LayerZero Customer", "1 Async Avenue", "Riga", "LV", "LV-1010"),
                new OrderScenario(ForcePaymentTimeoutOnce: true)),
            cancellationToken);

        Assert.True(created.IsSuccess);

        var fetched = await client.GetOrderForResponseAsync(created.Value.OrderId, cancellationToken);
        Assert.True(fetched.IsSuccess);
        Assert.Equal(created.Value.OrderId, fetched.Result.Value.Id);
        Assert.Equal(OrderStatuses.Draft, fetched.Result.Value.Status);

        var timeline = await client.GetTimelineAsync(created.Value.OrderId, cancellationToken);
        Assert.True(timeline.IsSuccess);
        Assert.Contains(timeline.Value, entry => entry.Step == "api.accepted");
    }

    [Fact]
    public async Task Explicit_typed_client_maps_validation_failures_to_layerzero_results()
    {
        var client = CreateClient();

        var response = await client.PlaceOrderAsync(
            new PlaceOrderApi.Request(
                string.Empty,
                [],
                new ShippingAddress(string.Empty, string.Empty, "Riga", "LV", "LV-1010"),
                new OrderScenario()),
            TestContext.Current.CancellationToken);

        Assert.True(response.IsFailure);
        Assert.Contains(response.Errors, error => error.Code == "layerzero.validation.not_empty");
    }

    [Fact]
    public async Task Explicit_typed_client_maps_not_found_without_throwing_and_exposes_headers()
    {
        var client = CreateClient();

        var missing = await client.GetOrderForResponseAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(missing.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains(missing.Result.Errors, error => error.Code == "layerzero.http.status.404");
        Assert.Null(missing.Problem);
    }

    [Fact]
    public void Typed_client_registration_keeps_ihttpclientbuilder_chaining_available()
    {
        var services = new ServiceCollection();
        services.AddTransient<PassthroughHandler>();

        var builder = services.AddLayerZeroClient<FulfillmentClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7380");
        }).AddHttpMessageHandler<PassthroughHandler>();

        Assert.NotNull(builder);
    }

    private FulfillmentClient CreateClient()
    {
        return new FulfillmentClient(factory.CreateClient());
    }

    private sealed class PassthroughHandler : DelegatingHandler;
}

public sealed class FulfillmentTypedClientFactory : WebApplicationFactory<Program>
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"layerzero-fulfillment-client-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Messaging:DisableTransport", "true");
        builder.UseSetting("ConnectionStrings:Fulfillment", $"Data Source={databasePath}");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:DisableTransport"] = "true",
                ["ConnectionStrings:Fulfillment"] = $"Data Source={databasePath}",
            });
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
