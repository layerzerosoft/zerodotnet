namespace LayerZero.Messaging;

/// <summary>
/// Invokes validators and one discovered handler path for a message type.
/// </summary>
public interface IMessageHandlerInvoker
{
    /// <summary>
    /// Gets the message descriptor.
    /// </summary>
    MessageDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the deterministic handler identity.
    /// </summary>
    string HandlerIdentity { get; }

    /// <summary>
    /// Gets whether this handler path requires idempotency support.
    /// </summary>
    bool RequiresIdempotency { get; }

    /// <summary>
    /// Invokes validators and handlers for a message instance.
    /// </summary>
    /// <param name="services">The scoped service provider.</param>
    /// <param name="message">The deserialized message instance.</param>
    /// <param name="context">The message context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The message handling result.</returns>
    ValueTask<MessageHandlingResult> InvokeAsync(
        IServiceProvider services,
        object message,
        MessageContext context,
        CancellationToken cancellationToken = default);
}
