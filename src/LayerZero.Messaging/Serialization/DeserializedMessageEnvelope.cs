namespace LayerZero.Messaging.Serialization;

internal sealed class DeserializedMessageEnvelope(
    MessageDescriptor descriptor,
    object message,
    MessageContext context)
{
    public MessageDescriptor Descriptor { get; } = descriptor;

    public object Message { get; } = message;

    public MessageContext Context { get; } = context;
}
