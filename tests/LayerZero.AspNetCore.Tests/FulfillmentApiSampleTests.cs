using System.Net;
using System.Text.Json.Nodes;
using LayerZero.Core;
using LayerZero.Fulfillment.Bootstrap;
using LayerZero.Fulfillment.Api.Features.Orders.Get;
using LayerZero.Fulfillment.Api.Features.Orders.Place;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Validation;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LayerZero.AspNetCore.Tests;

public sealed class FulfillmentApiSampleTests : IClassFixture<FulfillmentApiFactory>
{
    private readonly FulfillmentApiFactory factory;

    public FulfillmentApiSampleTests(FulfillmentApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Order_endpoints_are_mapped_by_generated_slice_extensions()
    {
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                customerEmail = "customer@example.com",
                items = new[] { new { sku = "LZ-CORE", quantity = 2 } },
                shippingAddress = new
                {
                    recipient = "LayerZero Customer",
                    line1 = "1 Async Avenue",
                    city = "Riga",
                    countryCode = "LV",
                    postalCode = "LV-1010",
                },
                scenario = new { forcePaymentTimeoutOnce = true },
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.StartsWith("/orders/", response.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        Assert.NotEqual(Guid.Empty, body["orderId"]?.GetValue<Guid>());
    }

    [Fact]
    public void Generated_add_slices_registers_handlers_and_validators()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetService<IAsyncRequestHandler<PlaceOrderApi.Request, PlaceOrderApi.Accepted>>());
        Assert.NotNull(services.GetService<IAsyncRequestHandler<GetOrderApi.Request, OrderDetails>>());
        Assert.NotEmpty(services.GetServices<IValidator<PlaceOrderApi.Request>>());
        Assert.NotNull(services.GetService<PlaceOrderEndpoint.Handler>());
        Assert.NotNull(services.GetService<GetOrderEndpoint.Handler>());
    }

    [Fact]
    public async Task Validation_returns_problem_details_with_layerzero_errors()
    {
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                customerEmail = "",
                items = Array.Empty<object>(),
                shippingAddress = new
                {
                    recipient = "",
                    line1 = "",
                    city = "Riga",
                    countryCode = "LV",
                    postalCode = "LV-1010",
                },
                scenario = new { },
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        Assert.Equal("Validation failed.", body["title"]?.GetValue<string>());
        Assert.NotNull(body["errors"]?["CustomerEmail"]);
        Assert.NotNull(body["layerzero.errors"]);
    }

    [Fact]
    public async Task Native_minimal_api_binding_services_links_and_query_values_still_work()
    {
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var orderId = await PlaceOrderAsync(client, cancellationToken);

        var order = await client.GetFromJsonAsync<JsonObject>($"/orders/{orderId}", cancellationToken)
            ?? throw new InvalidOperationException("Order response was empty.");
        Assert.Equal(orderId, order["id"]?.GetValue<Guid>());
        Assert.Equal("draft", order["status"]?.GetValue<string>());

        var timeline = await client.GetFromJsonAsync<JsonArray>($"/orders/{orderId}/timeline", cancellationToken)
            ?? throw new InvalidOperationException("Timeline response was empty.");
        Assert.NotEmpty(timeline);
        Assert.Contains(timeline, static node => node?["step"]?.GetValue<string>() == "api.accepted");

        var missing = await client.GetAsync($"/orders/{Guid.NewGuid()}", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_includes_self_mapped_endpoints_without_swashbuckle_or_nswag()
    {
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        var document = await client.GetStringAsync("/openapi/v1.json", cancellationToken);
        var openApi = JsonNode.Parse(document)!.AsObject();

        var openApiVersion = openApi["openapi"]?.GetValue<string>();
        Assert.NotNull(openApiVersion);
        Assert.StartsWith("3.1.", openApiVersion, StringComparison.Ordinal);
        Assert.Contains("\"/orders\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/orders/{id}\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/orders/{id}/timeline\"", document, StringComparison.Ordinal);
        Assert.Contains("\"/deadletters\"", document, StringComparison.Ordinal);
        Assert.DoesNotContain("Swashbuckle", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NSwag", document, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> PlaceOrderAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                customerEmail = "customer@example.com",
                items = new[] { new { sku = "LZ-CORE", quantity = 1 } },
                shippingAddress = new
                {
                    recipient = "LayerZero Customer",
                    line1 = "1 Async Avenue",
                    city = "Riga",
                    countryCode = "LV",
                    postalCode = "LV-1010",
                },
                scenario = new { },
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!;
        return body["orderId"]!.GetValue<Guid>();
    }
}

public sealed class FulfillmentApiFactory : WebApplicationFactory<Program>
{
    private readonly object initializationGate = new();
    private PostgreSqlContainer? container;
    private string? connectionString;
    private bool initialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        EnsureInitialized();

        builder.UseEnvironment("Development");
        builder.UseSetting("Messaging:DisableTransport", "true");
        builder.UseSetting("ConnectionStrings:Fulfillment", connectionString);
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:DisableTransport"] = "true",
                ["ConnectionStrings:Fulfillment"] = connectionString,
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && container is not null)
        {
            container.DisposeAsync().AsTask().GetAwaiter().GetResult();
            container = null;
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        lock (initializationGate)
        {
            if (initialized)
            {
                return;
            }

            container = new PostgreSqlBuilder("postgres:16.4").Build();
            container.StartAsync().GetAwaiter().GetResult();

            var databaseName = $"lz_{Guid.NewGuid():N}";
            using (var connection = new NpgsqlConnection(container.GetConnectionString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"""create database "{databaseName}";""";
                command.ExecuteNonQuery();
            }

            connectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
            {
                Database = databaseName,
            }.ConnectionString;

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Fulfillment"] = connectionString,
                })
                .Build();

            FulfillmentBootstrapHost.ApplyMigrationsAsync(configuration).GetAwaiter().GetResult();
            initialized = true;
        }
    }
}
