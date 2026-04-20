using System.Net;
using LayerZero.Core;
using LayerZero.Fulfillment.Client.Sample.Clients;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.RabbitMq.Api;
using LayerZero.Fulfillment.RabbitMq.Bootstrap;
using LayerZero.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

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
            client.BaseAddress = new Uri("https://localhost:7381");
        }).AddHttpMessageHandler<PassthroughHandler>();

        Assert.NotNull(builder);
    }

    private FulfillmentClient CreateClient()
    {
        return new FulfillmentClient(factory.CreateClient());
    }

    private sealed class PassthroughHandler : DelegatingHandler;
}

public sealed class FulfillmentTypedClientFactory : WebApplicationFactory<RabbitMqFulfillmentApiEntryPoint>
{
    private readonly object initializationGate = new();
    private PostgreSqlContainer? container;
    private string? connectionString;
    private bool initialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        EnsureInitialized();

        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Fulfillment", connectionString);
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Fulfillment"] = connectionString,
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICommandSender>();
            services.AddSingleton<ICommandSender, StubCommandSender>();
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
                    ["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@localhost:5672/",
                })
                .Build();

            using var host = CreateBootstrapHost(configuration);
            RabbitMqFulfillmentBootstrapHost.ApplyMigrationsAsync(host.Services).GetAwaiter().GetResult();
            initialized = true;
        }
    }

    private static IHost CreateBootstrapHost(IConfiguration configuration)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(configuration);
        RabbitMqFulfillmentBootstrapHost.ConfigureServices(builder.Services, builder.Configuration);
        return builder.Build();
    }

    private sealed class StubCommandSender : ICommandSender
    {
        public ValueTask<Result> SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
            where TCommand : class, ICommand
        {
            return ValueTask.FromResult(Result.Success());
        }
    }
}
