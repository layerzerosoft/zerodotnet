using Confluent.Kafka;
using LayerZero.Messaging.Kafka.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Kafka;

internal sealed class KafkaClientProvider(
    string name,
    IOptionsMonitor<KafkaBusOptions> optionsMonitor,
    ILogger<KafkaClientProvider> logger) : IDisposable, IAsyncDisposable
{
    private readonly string busName = name;
    private readonly IOptionsMonitor<KafkaBusOptions> optionsMonitor = optionsMonitor;
    private readonly ILogger<KafkaClientProvider> logger = logger;
    private IProducer<string, byte[]>? producer;
    private IAdminClient? adminClient;
    private bool disposed;

    public KafkaBusOptions Options => optionsMonitor.Get(busName);

    public IProducer<string, byte[]> GetProducer()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        producer ??= new ProducerBuilder<string, byte[]>(new ProducerConfig
        {
            BootstrapServers = Options.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
        })
            .SetLogHandler(static (_, _) => { })
            .SetErrorHandler((_, error) => LogKafkaError("producer", error))
            .Build();

        return producer;
    }

    public IAdminClient GetAdminClient()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        adminClient ??= new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = Options.BootstrapServers,
        })
            .SetLogHandler(static (_, _) => { })
            .SetErrorHandler((_, error) => LogKafkaError("admin", error))
            .Build();

        return adminClient;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            producer?.Flush(TimeSpan.FromSeconds(5));
        }
        catch
        {
        }

        producer?.Dispose();
        adminClient?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void LogKafkaError(string clientType, Error error)
    {
        if (error.IsFatal)
        {
            logger.LogError(
                "Kafka {ClientType} client reported a fatal error for bus '{BusName}': {Code} {Reason}",
                clientType,
                busName,
                error.Code,
                error.Reason);
            return;
        }

        logger.LogDebug(
            "Kafka {ClientType} client event for bus '{BusName}': {Code} {Reason}",
            clientType,
            busName,
            error.Code,
            error.Reason);
    }
}
