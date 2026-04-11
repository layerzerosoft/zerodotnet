using Confluent.Kafka;

namespace LayerZero.Messaging.Kafka;

internal sealed class KafkaMessageBusTransport(
    string name,
    KafkaClientProvider clientProvider,
    IMessageConventions conventions) : IMessageBusTransport
{
    public string Name { get; } = name;

    public ValueTask SendAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return PublishCoreAsync(message, cancellationToken);
    }

    public ValueTask PublishAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return PublishCoreAsync(message, cancellationToken);
    }

    private async ValueTask PublishCoreAsync(TransportMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var kafkaMessage = new Message<string, byte[]>
        {
            Key = message.Context.AffinityKey ?? message.Context.MessageId,
            Value = message.Body.ToArray(),
            Headers = new Headers
            {
                { "layerzero-message-name", System.Text.Encoding.UTF8.GetBytes(message.Context.MessageName) },
                { "layerzero-message-kind", System.Text.Encoding.UTF8.GetBytes(message.Context.MessageKind == MessageKind.Command ? "command" : "event") },
            },
        };

        if (message.Context.TraceParent is not null)
        {
            kafkaMessage.Headers.Add("traceparent", System.Text.Encoding.UTF8.GetBytes(message.Context.TraceParent));
        }

        if (message.Context.TraceState is not null)
        {
            kafkaMessage.Headers.Add("tracestate", System.Text.Encoding.UTF8.GetBytes(message.Context.TraceState));
        }

        await clientProvider.GetProducer()
            .ProduceAsync(conventions.GetEntityName(message.Descriptor), kafkaMessage, cancellationToken)
            .ConfigureAwait(false);
    }
}
