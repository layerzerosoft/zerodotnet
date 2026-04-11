using Confluent.Kafka;
using LayerZero.Messaging.Kafka.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Kafka;

internal sealed class KafkaClientProvider(string name, IOptionsMonitor<KafkaBusOptions> optionsMonitor) : IDisposable, IAsyncDisposable
{
    private readonly string busName = name;
    private readonly IOptionsMonitor<KafkaBusOptions> optionsMonitor = optionsMonitor;
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
        }).Build();

        return producer;
    }

    public IAdminClient GetAdminClient()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        adminClient ??= new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = Options.BootstrapServers,
        }).Build();

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
}
