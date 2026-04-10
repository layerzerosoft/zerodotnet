namespace LayerZero.Messaging;

/// <summary>
/// Invokes validators and handlers for one discovered message type.
/// </summary>
public interface IMessageHandlerInvoker
{
    /// <summary>
    /// Gets the message descriptor.
    /// </summary>
    MessageDescriptor Descriptor { get; }

    /// <summary>
    /// Gets whether any handler path requires idempotency support.
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
