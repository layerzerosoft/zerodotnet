namespace LayerZero.Messaging;

/// <summary>
/// Describes the generated topology metadata for one message.
/// </summary>
public sealed class MessageTopologyDescriptor
{
    /// <summary>
    /// Initializes a new <see cref="MessageTopologyDescriptor"/>.
    /// </summary>
    /// <param name="message">The message descriptor.</param>
    /// <param name="subscriptions">The generated subscriptions.</param>
    public MessageTopologyDescriptor(
        MessageDescriptor message,
        IReadOnlyList<MessageSubscriptionDescriptor>? subscriptions = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        Message = message;
        Subscriptions = subscriptions ?? [];
    }

    /// <summary>
    /// Gets the underlying message descriptor.
    /// </summary>
    public MessageDescriptor Message { get; }

    /// <summary>
    /// Gets the generated subscriptions for this message.
    /// </summary>
    public IReadOnlyList<MessageSubscriptionDescriptor> Subscriptions { get; }

    /// <summary>
    /// Gets whether any path requires idempotency.
    /// </summary>
    public bool RequiresIdempotency => Message.RequiresIdempotency || Subscriptions.Any(static subscription => subscription.RequiresIdempotency);
}
