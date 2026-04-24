using System.Diagnostics;
using LayerZero.Data;
using LayerZero.Data.Configuration;
using LayerZero.Data.Postgres;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Fulfillment.AzureServiceBus.Api;
using LayerZero.Fulfillment.AzureServiceBus.Bootstrap;
using LayerZero.Fulfillment.AzureServiceBus.Processing;
using LayerZero.Fulfillment.AzureServiceBus.Projections;
using LayerZero.Fulfillment.Kafka.Api;
using LayerZero.Fulfillment.Kafka.Bootstrap;
using LayerZero.Fulfillment.Kafka.Processing;
using LayerZero.Fulfillment.Kafka.Projections;
using LayerZero.Fulfillment.Nats.Api;
using LayerZero.Fulfillment.Nats.Bootstrap;
using LayerZero.Fulfillment.Nats.Processing;
using LayerZero.Fulfillment.Nats.Projections;
using LayerZero.Fulfillment.RabbitMq.Api;
using LayerZero.Fulfillment.RabbitMq.Bootstrap;
using LayerZero.Fulfillment.RabbitMq.Processing;
using LayerZero.Fulfillment.RabbitMq.Projections;
using LayerZero.Fulfillment.Shared;
using LayerZero.Messaging;
using LayerZero.Messaging.AzureServiceBus;
using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.Kafka;
using LayerZero.Messaging.Nats;
using LayerZero.Messaging.Operations;
using LayerZero.Messaging.Operations.Postgres;
using LayerZero.Messaging.RabbitMq;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using LayerZero.Migrations;

namespace LayerZero.Fulfillment.EndToEnd.Tests;

public sealed class FulfillmentOrderRun
{
    public FulfillmentOrderRun(Guid orderId, string traceParent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceParent);

        OrderId = orderId;
        TraceParent = traceParent;
        TraceId = ExtractTraceId(traceParent);
    }

    public Guid OrderId { get; }

    public string TraceParent { get; }

    public string TraceId { get; }

    public bool MatchesTraceParent(string? traceParent)
    {
        var candidateTraceId = TryExtractTraceId(traceParent);
        return candidateTraceId is not null
            && string.Equals(TraceId, candidateTraceId, StringComparison.Ordinal);
    }

    private static string ExtractTraceId(string traceParent)
    {
        return TryExtractTraceId(traceParent)
            ?? throw new ArgumentException("Trace parent must be a valid W3C traceparent value.", nameof(traceParent));
    }

    private static string? TryExtractTraceId(string? traceParent)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return null;
        }

        var segments = traceParent.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 4 && segments[1].Length == 32
            ? segments[1]
            : null;
    }
}

public sealed class FulfillmentHarness : IAsyncDisposable
{
    private readonly FulfillmentPostgresDatabase database;
    private readonly IHost processingHost;
    private readonly IHost projectionHost;
    private readonly IFulfillmentApiFactory factory;

    private FulfillmentHarness(
        FulfillmentPostgresDatabase database,
        IHost processingHost,
        IHost projectionHost,
        IFulfillmentApiFactory factory,
        HttpClient client)
    {
        this.database = database;
        this.processingHost = processingHost;
        this.projectionHost = projectionHost;
        this.factory = factory;
        Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<FulfillmentHarness> CreateAsync(IFulfillmentBrokerFixture brokerFixture, CancellationToken cancellationToken = default)
    {
        var database = await FulfillmentPostgresDatabase.CreateAsync(cancellationToken).ConfigureAwait(false);
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionStrings:Fulfillment"] = database.ConnectionString,
            ["Messaging:ApplicationName"] = $"fulfillment-e2e-{Guid.NewGuid():N}",
        };

        brokerFixture.ApplyConfiguration(settings);

        using (var bootstrapHost = CreateBootstrapHost(brokerFixture.BrokerName, settings))
        {
            await ApplyBootstrapAsync(bootstrapHost, cancellationToken).ConfigureAwait(false);
        }

        var processingHost = CreateProcessingHost(brokerFixture.BrokerName, settings);
        var projectionHost = CreateProjectionsHost(brokerFixture.BrokerName, settings);

        await processingHost.StartAsync(cancellationToken).ConfigureAwait(false);
        await projectionHost.StartAsync(cancellationToken).ConfigureAwait(false);

        var factory = CreateApiFactory(brokerFixture.BrokerName, settings);
        var client = factory.CreateClient();

        return new FulfillmentHarness(database, processingHost, projectionHost, factory, client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await projectionHost.StopAsync().ConfigureAwait(false);
        await processingHost.StopAsync().ConfigureAwait(false);
        await DisposeAsync(projectionHost).ConfigureAwait(false);
        await DisposeAsync(processingHost).ConfigureAwait(false);
        await DisposeAsync(factory).ConfigureAwait(false);
        await database.DisposeAsync().ConfigureAwait(false);
    }

    public async Task<FulfillmentOrderRun> PlaceOrderAsync(OrderScenario scenario, string? traceParent = null, CancellationToken cancellationToken = default)
    {
        var effectiveTraceParent = string.IsNullOrWhiteSpace(traceParent)
            ? CreateTraceParent()
            : traceParent;

        var request = new PlaceOrderApi.Request(
            "customer@example.com",
            [new OrderItem("LZ-ASYNC", 1)],
            new ShippingAddress("LayerZero Customer", "1 Async Avenue", "Riga", "LV", "LV-1010"),
            scenario);

        using var message = new HttpRequestMessage(HttpMethod.Post, OrderRoutes.Collection)
        {
            Content = JsonContent.Create(request),
        };

        message.Headers.TryAddWithoutValidation("traceparent", effectiveTraceParent);

        var response = await Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var accepted = await response.Content.ReadFromJsonAsync<PlaceOrderApi.Accepted>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Order placement response was empty.");
        return new FulfillmentOrderRun(accepted.OrderId, effectiveTraceParent);
    }

    public async Task CancelOrderAsync(Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostAsJsonAsync(
            OrderRoutes.Cancel.Replace("{id:guid}", orderId.ToString(), StringComparison.Ordinal),
            new CancelOrderApi.Body(reason),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<OrderDetails> WaitForOrderAsync(FulfillmentOrderRun run, Func<OrderDetails, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var order = await Client.GetFromJsonAsync<OrderDetails>($"/orders/{run.OrderId}", cancellationToken).ConfigureAwait(false);
            if (order is not null && predicate(order))
            {
                return order;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for order '{run.OrderId}' to reach the expected state.");
    }

    public async Task<IReadOnlyList<OrderTimelineEntry>> WaitForTimelineAsync(FulfillmentOrderRun run, Func<IReadOnlyList<OrderTimelineEntry>, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timeline = await Client.GetFromJsonAsync<IReadOnlyList<OrderTimelineEntry>>($"/orders/{run.OrderId}/timeline", cancellationToken).ConfigureAwait(false) ?? [];
            if (predicate(timeline))
            {
                return timeline;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for timeline updates for order '{run.OrderId}'.");
    }

    public async Task<IReadOnlyList<DeadLetterRecord>> WaitForDeadLettersAsync(FulfillmentOrderRun run, Func<IReadOnlyList<DeadLetterRecord>, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var records = await Client.GetFromJsonAsync<IReadOnlyList<DeadLetterRecord>>(OrderRoutes.DeadLetters, cancellationToken).ConfigureAwait(false) ?? [];
            var matchingRecords = records.Where(record => run.MatchesTraceParent(record.TraceParent)).ToArray();
            if (predicate(matchingRecords))
            {
                return matchingRecords;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for dead-letter records for order '{run.OrderId}'.");
    }

    private static string CreateTraceParent()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        return $"00-{traceId}-{spanId}-01";
    }

    private static IHost CreateBootstrapHost(string brokerName, IReadOnlyDictionary<string, string?> settings)
    {
        var builder = CreateHostBuilder(brokerName, settings);
        ConfigureBootstrapServices(brokerName, builder.Services, builder.Configuration);
        return builder.Build();
    }

    private static async Task ApplyBootstrapAsync(IHost host, CancellationToken cancellationToken)
    {
        await host.Services.GetRequiredService<IMigrationRuntime>()
            .ApplyAsync(cancellationToken: cancellationToken)
            .AsTask()
            .ConfigureAwait(false);
        await host.Services.GetRequiredService<IMessageTopologyProvisioner>()
            .ProvisionAsync(cancellationToken)
            .AsTask()
            .ConfigureAwait(false);
    }

    private static IHost CreateProcessingHost(string brokerName, IReadOnlyDictionary<string, string?> settings)
    {
        var builder = CreateHostBuilder(brokerName, settings);
        ConfigureProcessingServices(brokerName, builder.Services, builder.Configuration);
        return builder.Build();
    }

    private static IHost CreateProjectionsHost(string brokerName, IReadOnlyDictionary<string, string?> settings)
    {
        var builder = CreateHostBuilder(brokerName, settings);
        ConfigureProjectionsServices(brokerName, builder.Services, builder.Configuration);
        return builder.Build();
    }

    private static HostApplicationBuilder CreateHostBuilder(string brokerName, IReadOnlyDictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Environment.ApplicationName = GetApplicationName(brokerName);
        builder.Configuration.AddInMemoryCollection(settings);
        return builder;
    }

    private static void ConfigureBootstrapServices(string brokerName, IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
        ConfigureBootstrapData(brokerName, services.AddData<FulfillmentStore>().UsePostgres("Fulfillment"));
        services.AddMessagingOperations().UsePostgres("Fulfillment");
        ConfigureMessagingTransport(brokerName, CreateBootstrapMessagingBuilder(brokerName, services), configuration, MessageTransportRole.Administration);
    }

    private static void ConfigureProcessingServices(string brokerName, IServiceCollection services, IConfiguration configuration)
    {
        ConfigureWorkerServices(services);
        ConfigureMessagingTransport(brokerName, CreateProcessingMessagingBuilder(brokerName, services), configuration, MessageTransportRole.Consumers);
    }

    private static void ConfigureProjectionsServices(string brokerName, IServiceCollection services, IConfiguration configuration)
    {
        ConfigureWorkerServices(services);
        ConfigureMessagingTransport(brokerName, CreateProjectionsMessagingBuilder(brokerName, services), configuration, MessageTransportRole.Consumers);
    }

    private static void ConfigureWorkerServices(IServiceCollection services)
    {
        services.AddLogging(logging => logging.AddSimpleConsole(static options => options.SingleLine = true));
        services.AddData<FulfillmentStore>().UsePostgres("Fulfillment");
        services.AddMessagingOperations().UsePostgres("Fulfillment");
        services.AddFulfillmentStore();
    }

    private static void ConfigureMessagingTransport(
        string brokerName,
        MessagingBuilder messaging,
        IConfiguration configuration,
        MessageTransportRole role)
    {
        switch (brokerName)
        {
            case "AzureServiceBus":
                messaging.AddAzureServiceBus(configuration, role: role);
                break;
            case "RabbitMq":
                messaging.AddRabbitMq(configuration, role: role);
                break;
            case "Kafka":
                messaging.AddKafka(configuration, role: role);
                break;
            case "Nats":
                messaging.AddNats(configuration, role: role);
                break;
            default:
                throw new InvalidOperationException($"Unsupported broker '{brokerName}'.");
        }
    }

    private static void ConfigureBootstrapData(string brokerName, DataBuilder data)
    {
        switch (brokerName)
        {
            case "AzureServiceBus":
                data.UseMigrations<AzureServiceBusFulfillmentBootstrapEntryPoint>(options => options.Executor = GetBootstrapExecutor(brokerName));
                break;
            case "RabbitMq":
                data.UseMigrations<RabbitMqFulfillmentBootstrapEntryPoint>(options => options.Executor = GetBootstrapExecutor(brokerName));
                break;
            case "Kafka":
                data.UseMigrations<KafkaFulfillmentBootstrapEntryPoint>(options => options.Executor = GetBootstrapExecutor(brokerName));
                break;
            case "Nats":
                data.UseMigrations<NatsFulfillmentBootstrapEntryPoint>(options => options.Executor = GetBootstrapExecutor(brokerName));
                break;
            default:
                throw new InvalidOperationException($"Unsupported broker '{brokerName}'.");
        }
    }

    private static MessagingBuilder CreateBootstrapMessagingBuilder(string brokerName, IServiceCollection services)
    {
        return brokerName switch
        {
            "AzureServiceBus" => services.AddMessaging<AzureServiceBusFulfillmentBootstrapEntryPoint>(),
            "RabbitMq" => services.AddMessaging<RabbitMqFulfillmentBootstrapEntryPoint>(),
            "Kafka" => services.AddMessaging<KafkaFulfillmentBootstrapEntryPoint>(),
            "Nats" => services.AddMessaging<NatsFulfillmentBootstrapEntryPoint>(),
            _ => throw new InvalidOperationException($"Unsupported broker '{brokerName}'."),
        };
    }

    private static MessagingBuilder CreateProcessingMessagingBuilder(string brokerName, IServiceCollection services)
    {
        return brokerName switch
        {
            "AzureServiceBus" => services.AddMessaging<AzureServiceBusFulfillmentProcessingEntryPoint>(),
            "RabbitMq" => services.AddMessaging<RabbitMqFulfillmentProcessingEntryPoint>(),
            "Kafka" => services.AddMessaging<KafkaFulfillmentProcessingEntryPoint>(),
            "Nats" => services.AddMessaging<NatsFulfillmentProcessingEntryPoint>(),
            _ => throw new InvalidOperationException($"Unsupported broker '{brokerName}'."),
        };
    }

    private static MessagingBuilder CreateProjectionsMessagingBuilder(string brokerName, IServiceCollection services)
    {
        return brokerName switch
        {
            "AzureServiceBus" => services.AddMessaging<AzureServiceBusFulfillmentProjectionsEntryPoint>(),
            "RabbitMq" => services.AddMessaging<RabbitMqFulfillmentProjectionsEntryPoint>(),
            "Kafka" => services.AddMessaging<KafkaFulfillmentProjectionsEntryPoint>(),
            "Nats" => services.AddMessaging<NatsFulfillmentProjectionsEntryPoint>(),
            _ => throw new InvalidOperationException($"Unsupported broker '{brokerName}'."),
        };
    }

    private static string GetApplicationName(string brokerName)
    {
        switch (brokerName)
        {
            case "AzureServiceBus":
                return "fulfillment-azure-service-bus";
            case "RabbitMq":
                return "fulfillment-rabbitmq";
            case "Kafka":
                return "fulfillment-kafka";
            case "Nats":
                return "fulfillment-nats";
            default:
                throw new InvalidOperationException($"Unsupported broker '{brokerName}'.");
        }
    }

    private static string GetBootstrapExecutor(string brokerName)
    {
        switch (brokerName)
        {
            case "AzureServiceBus":
                return "fulfillment-azure-service-bus-bootstrap";
            case "RabbitMq":
                return "fulfillment-rabbitmq-bootstrap";
            case "Kafka":
                return "fulfillment-kafka-bootstrap";
            case "Nats":
                return "fulfillment-nats-bootstrap";
            default:
                throw new InvalidOperationException($"Unsupported broker '{brokerName}'.");
        }
    }

    private static IFulfillmentApiFactory CreateApiFactory(string brokerName, IReadOnlyDictionary<string, string?> settings)
    {
        return brokerName switch
        {
            "AzureServiceBus" => new AzureServiceBusFulfillmentApiFactory(settings),
            "RabbitMq" => new RabbitMqFulfillmentApiFactory(settings),
            "Kafka" => new KafkaFulfillmentApiFactory(settings),
            "Nats" => new NatsFulfillmentApiFactory(settings),
            _ => throw new InvalidOperationException($"Unsupported broker '{brokerName}'."),
        };
    }

    private static async ValueTask DisposeAsync(object instance)
    {
        switch (instance)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    private interface IFulfillmentApiFactory
    {
        IServiceProvider Services { get; }

        HttpClient CreateClient();
    }

    private abstract class FulfillmentApiFactory<TEntryPoint>(IReadOnlyDictionary<string, string?> settings)
        : WebApplicationFactory<TEntryPoint>, IFulfillmentApiFactory
        where TEntryPoint : class
    {
        protected IReadOnlyDictionary<string, string?> Settings { get; } = settings;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            foreach (var setting in Settings)
            {
                if (!string.IsNullOrWhiteSpace(setting.Value))
                {
                    builder.UseSetting(setting.Key, setting.Value);
                }
            }

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(Settings);
            });
        }
    }

    private sealed class RabbitMqFulfillmentApiFactory(IReadOnlyDictionary<string, string?> settings)
        : FulfillmentApiFactory<RabbitMqFulfillmentApiEntryPoint>(settings);

    private sealed class AzureServiceBusFulfillmentApiFactory(IReadOnlyDictionary<string, string?> settings)
        : FulfillmentApiFactory<AzureServiceBusFulfillmentApiEntryPoint>(settings);

    private sealed class KafkaFulfillmentApiFactory(IReadOnlyDictionary<string, string?> settings)
        : FulfillmentApiFactory<KafkaFulfillmentApiEntryPoint>(settings);

    private sealed class NatsFulfillmentApiFactory(IReadOnlyDictionary<string, string?> settings)
        : FulfillmentApiFactory<NatsFulfillmentApiEntryPoint>(settings);
}

internal sealed class FulfillmentPostgresDatabase : IAsyncDisposable
{
    private FulfillmentPostgresDatabase(PostgreSqlContainer container, string connectionString)
    {
        Container = container;
        ConnectionString = connectionString;
    }

    public PostgreSqlContainer Container { get; }

    public string ConnectionString { get; }

    public static async Task<FulfillmentPostgresDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var container = new PostgreSqlBuilder("postgres:16.4").Build();
        await container.StartAsync(cancellationToken).ConfigureAwait(false);

        var databaseName = $"lz_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""create database "{databaseName}";""";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var builder = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = databaseName,
        };

        return new FulfillmentPostgresDatabase(container, builder.ConnectionString);
    }

    public async ValueTask DisposeAsync() => await Container.DisposeAsync().ConfigureAwait(false);
}
