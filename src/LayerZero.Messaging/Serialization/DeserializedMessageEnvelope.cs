namespace LayerZero.Messaging.Serialization;

/// <summary>
/// Represents a deserialized transport envelope.
/// </summary>
public sealed class DeserializedMessageEnvelope(
    MessageDescriptor descriptor,
    object message,
    MessageContext context)
{
    /// <summary>
    /// Gets the message descriptor.
    /// </summary>
    public MessageDescriptor Descriptor { get; } = descriptor;

    /// <summary>
    /// Gets the deserialized message payload.
    /// </summary>
    public object Message { get; } = message;

    /// <summary>
    /// Gets the envelope context.
    /// </summary>
    public MessageContext Context { get; } = context;
}
