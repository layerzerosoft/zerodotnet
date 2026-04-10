namespace LayerZero.Messaging;

/// <summary>
/// Represents a transport-ready message payload.
/// </summary>
public sealed class TransportMessage
{
    /// <summary>
    /// Initializes a new <see cref="TransportMessage"/>.
    /// </summary>
    /// <param name="descriptor">The message descriptor.</param>
    /// <param name="context">The message context.</param>
    /// <param name="body">The serialized message body.</param>
    public TransportMessage(
        MessageDescriptor descriptor,
        MessageContext context,
        ReadOnlyMemory<byte> body)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(context);

        Descriptor = descriptor;
        Context = context;
        Body = body;
    }

    /// <summary>
    /// Gets the message descriptor.
    /// </summary>
    public MessageDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the message context.
    /// </summary>
    public MessageContext Context { get; }

    /// <summary>
    /// Gets the serialized message body.
    /// </summary>
    public ReadOnlyMemory<byte> Body { get; }
}
