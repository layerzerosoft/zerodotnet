using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;

namespace LayerZero.Messaging.AzureServiceBus;

internal sealed class AzureServiceBusMessageBusTransport(
    string name,
    AzureServiceBusClientProvider clientProvider,
    IMessageConventions conventions) : IMessageBusTransport, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ServiceBusSender> senders = new(StringComparer.Ordinal);

    public string Name { get; } = name;

    public ValueTask SendAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return PublishCoreAsync(message, cancellationToken);
    }

    public ValueTask PublishAsync(TransportMessage message, CancellationToken cancellationToken = default)
    {
        return PublishCoreAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in senders.Values)
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask PublishCoreAsync(TransportMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var entityName = AzureServiceBusNameFormatter.FormatEntityName(conventions.GetEntityName(message.Descriptor));
        var sender = senders.GetOrAdd(entityName, static (name, provider) => provider.GetClient().CreateSender(name), clientProvider);

        var busMessage = new ServiceBusMessage(message.Body)
        {
            MessageId = message.Context.MessageId,
            CorrelationId = message.Context.CorrelationId,
            Subject = message.Context.MessageName,
            ContentType = "application/json",
            SessionId = message.Context.AffinityKey,
        };

        busMessage.ApplicationProperties["layerzero-message-kind"] = message.Context.MessageKind == MessageKind.Command ? "command" : "event";
        busMessage.ApplicationProperties["layerzero-attempt"] = message.Context.Attempt;

        if (message.Context.TraceParent is not null)
        {
            busMessage.ApplicationProperties["traceparent"] = message.Context.TraceParent;
        }

        if (message.Context.TraceState is not null)
        {
            busMessage.ApplicationProperties["tracestate"] = message.Context.TraceState;
        }

        await sender.SendMessageAsync(busMessage, cancellationToken).ConfigureAwait(false);
    }
}
