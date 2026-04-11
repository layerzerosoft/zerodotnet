using System.Buffers;
using System.Text.Json;

namespace LayerZero.Messaging.Serialization;

/// <summary>
/// Serializes and deserializes LayerZero transport envelopes.
/// </summary>
public sealed class MessageEnvelopeSerializer
{
    private const string MessageIdProperty = "messageId";
    private const string MessageNameProperty = "messageName";
    private const string MessageKindProperty = "messageKind";
    private const string CorrelationIdProperty = "correlationId";
    private const string CausationIdProperty = "causationId";
    private const string TraceParentProperty = "traceParent";
    private const string TraceStateProperty = "traceState";
    private const string TimestampProperty = "timestamp";
    private const string AttemptProperty = "attempt";
    private const string AffinityKeyProperty = "affinityKey";
    private const string HeadersProperty = "headers";
    private const string PayloadProperty = "payload";

    /// <summary>
    /// Serializes one message and its envelope metadata.
    /// </summary>
    /// <param name="descriptor">The message descriptor.</param>
    /// <param name="message">The message payload.</param>
    /// <param name="context">The message context.</param>
    /// <returns>The serialized envelope body.</returns>
    public ReadOnlyMemory<byte> Serialize(MessageDescriptor descriptor, object message, MessageContext context)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartObject();
        writer.WriteString(MessageIdProperty, context.MessageId);
        writer.WriteString(MessageNameProperty, descriptor.Name);
        writer.WriteString(MessageKindProperty, descriptor.Kind == MessageKind.Command ? "command" : "event");

        if (context.CorrelationId is not null)
        {
            writer.WriteString(CorrelationIdProperty, context.CorrelationId);
        }

        if (context.CausationId is not null)
        {
            writer.WriteString(CausationIdProperty, context.CausationId);
        }

        if (context.TraceParent is not null)
        {
            writer.WriteString(TraceParentProperty, context.TraceParent);
        }

        if (context.TraceState is not null)
        {
            writer.WriteString(TraceStateProperty, context.TraceState);
        }

        writer.WriteString(TimestampProperty, context.Timestamp);
        writer.WriteNumber(AttemptProperty, context.Attempt);

        if (context.AffinityKey is not null)
        {
            writer.WriteString(AffinityKeyProperty, context.AffinityKey);
        }

        writer.WritePropertyName(HeadersProperty);
        writer.WriteStartObject();

        foreach (var pair in context.Headers)
        {
            writer.WriteString(pair.Key, pair.Value);
        }

        writer.WriteEndObject();
        writer.WritePropertyName(PayloadProperty);
        JsonSerializer.Serialize(writer, message, descriptor.JsonTypeInfo);
        writer.WriteEndObject();
        writer.Flush();

        return buffer.WrittenMemory.ToArray();
    }

    /// <summary>
    /// Deserializes one incoming transport envelope.
    /// </summary>
    /// <param name="body">The transport body.</param>
    /// <param name="transportName">The transport name.</param>
    /// <param name="registry">The message registry.</param>
    /// <returns>The deserialized envelope.</returns>
    public DeserializedMessageEnvelope Deserialize(
        ReadOnlyMemory<byte> body,
        string transportName,
        IMessageRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
        ArgumentNullException.ThrowIfNull(registry);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var messageName = root.GetProperty(MessageNameProperty).GetString()
            ?? throw new InvalidOperationException("The incoming message did not include a messageName.");

        if (!registry.TryGetDescriptor(messageName, out var descriptor))
        {
            throw new InvalidOperationException($"Message '{messageName}' is not registered.");
        }

        var messageKind = ParseKind(root.GetProperty(MessageKindProperty).GetString());
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty(HeadersProperty, out var headersElement))
        {
            foreach (var property in headersElement.EnumerateObject())
            {
                headers[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        var context = new MessageContext(
            root.GetProperty(MessageIdProperty).GetString()
                ?? throw new InvalidOperationException("The incoming message did not include a messageId."),
            messageName,
            messageKind,
            transportName,
            TryGetString(root, CorrelationIdProperty),
            TryGetString(root, CausationIdProperty),
            TryGetString(root, TraceParentProperty),
            TryGetString(root, TraceStateProperty),
            root.GetProperty(TimestampProperty).GetDateTimeOffset(),
            root.GetProperty(AttemptProperty).GetInt32(),
            TryGetString(root, AffinityKeyProperty),
            headers);

        var payload = root.GetProperty(PayloadProperty);
        var deserialized = JsonSerializer.Deserialize(payload, descriptor.JsonTypeInfo)
            ?? throw new InvalidOperationException($"Message '{messageName}' could not be deserialized.");

        return new DeserializedMessageEnvelope(descriptor, deserialized, context);
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element) ? element.GetString() : null;
    }

    private static MessageKind ParseKind(string? kind)
    {
        return string.Equals(kind, "event", StringComparison.OrdinalIgnoreCase)
            ? MessageKind.Event
            : MessageKind.Command;
    }
}
