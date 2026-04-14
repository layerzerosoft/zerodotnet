using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;
var bootstrapServers = ResolveBootstrapServers(configuration);
var timeout = configuration.GetValue<TimeSpan?>("KafkaReadiness:Timeout") ?? TimeSpan.FromSeconds(45);
var retryDelay = configuration.GetValue<TimeSpan?>("KafkaReadiness:RetryDelay") ?? TimeSpan.FromMilliseconds(250);
var metadataTimeout = configuration.GetValue<TimeSpan?>("KafkaReadiness:MetadataTimeout") ?? TimeSpan.FromSeconds(5);

using var adminClient = new AdminClientBuilder(new AdminClientConfig
{
    BootstrapServers = bootstrapServers,
    SocketConnectionSetupTimeoutMs = (int)Math.Ceiling(metadataTimeout.TotalMilliseconds),
})
    .SetLogHandler(static (_, _) => { })
    .Build();

var deadline = DateTimeOffset.UtcNow + timeout;
Exception? lastException = null;

while (DateTimeOffset.UtcNow < deadline)
{
    try
    {
        await ProbeBrokerAsync(adminClient, bootstrapServers, metadataTimeout).ConfigureAwait(false);
        return;
    }
    catch (KafkaException exception) when (!exception.Error.IsFatal)
    {
        lastException = exception;
    }
    catch (Exception exception)
    {
        lastException = exception;
    }

    await Task.Delay(retryDelay).ConfigureAwait(false);
}

throw new InvalidOperationException(
    $"Kafka broker '{bootstrapServers}' did not become metadata-ready within {timeout}.",
    lastException);

static string ResolveBootstrapServers(IConfiguration configuration)
{
    var bootstrapServers = configuration.GetConnectionString("kafka")
        ?? configuration["Messaging:Kafka:BootstrapServers"];

    if (string.IsNullOrWhiteSpace(bootstrapServers))
    {
        throw new InvalidOperationException(
            "Kafka readiness requires ConnectionStrings:kafka or Messaging:Kafka:BootstrapServers.");
    }

    return bootstrapServers;
}

static async Task ProbeBrokerAsync(
    IAdminClient adminClient,
    string bootstrapServers,
    TimeSpan metadataTimeout)
{
    var metadata = adminClient.GetMetadata(metadataTimeout);
    if (metadata.Brokers.Count == 0)
    {
        throw new InvalidOperationException("Kafka broker metadata did not include any brokers.");
    }

    var topicName = $"layerzero-readiness-{Guid.NewGuid():N}";
    await adminClient.CreateTopicsAsync(
        [
            new TopicSpecification
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1,
            },
        ],
        new CreateTopicsOptions
        {
            RequestTimeout = metadataTimeout,
            OperationTimeout = metadataTimeout,
        }).ConfigureAwait(false);

    using var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig
    {
        BootstrapServers = bootstrapServers,
        Acks = Acks.All,
        MessageTimeoutMs = (int)Math.Ceiling(metadataTimeout.TotalMilliseconds),
        SocketConnectionSetupTimeoutMs = (int)Math.Ceiling(metadataTimeout.TotalMilliseconds),
    })
        .SetLogHandler(static (_, _) => { })
        .Build();

    try
    {
        await producer.ProduceAsync(topicName, new Message<string, byte[]>
        {
            Key = "readiness",
            Value = [],
        }).ConfigureAwait(false);
        producer.Flush(metadataTimeout);

        await adminClient.DeleteTopicsAsync(
            [topicName],
            new DeleteTopicsOptions
            {
                RequestTimeout = metadataTimeout,
                OperationTimeout = metadataTimeout,
            }).ConfigureAwait(false);
    }
    catch (DeleteTopicsException)
    {
        // The readiness probe only needs to prove the broker accepted admin traffic.
    }
}
