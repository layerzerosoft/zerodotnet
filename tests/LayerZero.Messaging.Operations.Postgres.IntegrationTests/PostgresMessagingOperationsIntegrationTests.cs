using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LayerZero.Core;
using LayerZero.Data;
using LayerZero.Data.Postgres;
using LayerZero.Messaging;
using LayerZero.Messaging.Operations;
using LayerZero.Messaging.Operations.Postgres;
using LayerZero.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LayerZero.Messaging.Operations.Postgres.IntegrationTests;

public sealed partial class PostgresMessagingOperationsIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16.4").Build();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Apply_and_replay_round_trip_dead_letter_records()
    {
        var transport = new FakeTransport();
        await using var provider = BuildProvider(transport);

        var runtime = provider.GetRequiredService<IMigrationRuntime>();
        await runtime.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(
            await ExecuteScalarAsync<bool>(
                container.GetConnectionString(),
                "select to_regclass('public.lz_dead_letters') is not null;",
                TestContext.Current.CancellationToken));

        var serializer = provider.GetRequiredService<Serialization.MessageEnvelopeSerializer>();
        var store = provider.GetRequiredService<IDeadLetterStore>();
        var observer = Assert.Single(provider.GetServices<IMessageSettlementObserver>());
        var descriptor = CreateDescriptor();
        var context = new MessageContext(
            "msg-1",
            descriptor.Name,
            MessageKind.Command,
            "primary",
            "corr-1",
            null,
            "00-11111111111111111111111111111111-2222222222222222-01",
            null,
            DateTimeOffset.UtcNow,
            2);
        var body = serializer.Serialize(descriptor, new TestCommand("settle-order"), context);

        await observer.OnSettledAsync(
            context,
            MessageProcessingAction.DeadLetter,
            "primary",
            "tests.handler",
            [Error.Create("layerzero.tests.dead_letter", "The handler failed.")],
            "Dead lettered for integration coverage.",
            body,
            TestContext.Current.CancellationToken);

        var records = await store.GetDeadLettersAsync(TestContext.Current.CancellationToken);
        Assert.Single(records);
        Assert.Equal("msg-1", records[0].MessageId);
        Assert.False(records[0].Requeued);

        var replayService = provider.GetRequiredService<IDeadLetterReplayService>();
        var requeued = await replayService.RequeueAsync("msg-1", "tests.handler", TestContext.Current.CancellationToken);

        Assert.True(requeued);
        Assert.Single(transport.SentMessages);

        var updated = await store.GetDeadLettersAsync(TestContext.Current.CancellationToken);
        Assert.True(updated[0].Requeued);
    }

    [Fact]
    public async Task Idempotency_store_supports_begin_complete_and_abandon_cycles()
    {
        await using var provider = BuildProvider(new FakeTransport());

        var runtime = provider.GetRequiredService<IMigrationRuntime>();
        await runtime.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        var store = provider.GetRequiredService<IMessageIdempotencyStore>();

        Assert.True(await store.TryBeginAsync("shipment:1", TestContext.Current.CancellationToken));

        await store.CompleteAsync("shipment:1", TestContext.Current.CancellationToken);
        Assert.False(await store.TryBeginAsync("shipment:1", TestContext.Current.CancellationToken));

        await store.AbandonAsync("shipment:1", TestContext.Current.CancellationToken);
        Assert.True(await store.TryBeginAsync("shipment:1", TestContext.Current.CancellationToken));
    }

    private ServiceProvider BuildProvider(FakeTransport transport)
    {
        var descriptor = CreateDescriptor();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Fulfillment"] = container.GetConnectionString(),
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddData(data =>
        {
            data.UsePostgres(options =>
            {
                options.ConnectionString = container.GetConnectionString();
                options.DefaultSchema = "public";
            });
            data.UseMigrations(options => options.Executor = "messaging-operations-tests");
        });

        services.AddMessaging("messaging-operations-tests");
        services.AddMessagingOperations().UsePostgres("Fulfillment");
        services.AddSingleton<IMessageRegistry>(new FakeRegistry(descriptor));
        services.AddSingleton(new MessageBusRegistration("primary", typeof(FakeTransport)));
        services.AddKeyedSingleton<IMessageBusTransport>("primary", (_, _) => transport);

        return services.BuildServiceProvider();
    }

    private static MessageDescriptor CreateDescriptor()
    {
        return new MessageDescriptor(
            MessageNames.For<TestCommand>(),
            typeof(TestCommand),
            MessageKind.Command,
            PostgresMessagingOperationsJsonContext.Default.GetTypeInfo(typeof(TestCommand))!,
            MessageTopologyNames.Entity(MessageKind.Command, MessageNames.For<TestCommand>()));
    }

    private static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    private sealed class FakeRegistry(MessageDescriptor descriptor) : IMessageRegistry
    {
        public IReadOnlyList<MessageDescriptor> Messages { get; } = [descriptor];

        public bool TryGetDescriptor(Type messageType, out MessageDescriptor resolved)
        {
            if (messageType == descriptor.MessageType)
            {
                resolved = descriptor;
                return true;
            }

            resolved = null!;
            return false;
        }

        public bool TryGetDescriptor(string messageName, out MessageDescriptor resolved)
        {
            if (string.Equals(messageName, descriptor.Name, StringComparison.Ordinal))
            {
                resolved = descriptor;
                return true;
            }

            resolved = null!;
            return false;
        }
    }

    private sealed class FakeTransport : IMessageBusTransport
    {
        public List<TransportMessage> SentMessages { get; } = [];

        public string Name => "primary";

        public ValueTask SendAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync(TransportMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return ValueTask.CompletedTask;
        }
    }

    private sealed record TestCommand(string Title) : ICommand;

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(TestCommand))]
    private sealed partial class PostgresMessagingOperationsJsonContext : JsonSerializerContext;
}
