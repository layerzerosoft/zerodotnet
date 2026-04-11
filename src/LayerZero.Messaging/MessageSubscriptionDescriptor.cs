namespace LayerZero.Messaging;

/// <summary>
/// Describes one durable message subscription or consumer path.
/// </summary>
public sealed class MessageSubscriptionDescriptor
{
    /// <summary>
    /// Initializes a new <see cref="MessageSubscriptionDescriptor"/>.
    /// </summary>
    /// <param name="identity">The deterministic handler identity.</param>
    /// <param name="handlerType">The CLR handler type.</param>
    /// <param name="requiresIdempotency">Whether this handler requires idempotency support.</param>
    public MessageSubscriptionDescriptor(
        string identity,
        Type handlerType,
        bool requiresIdempotency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentNullException.ThrowIfNull(handlerType);

        Identity = identity;
        HandlerType = handlerType;
        RequiresIdempotency = requiresIdempotency;
    }

    /// <summary>
    /// Gets the deterministic handler identity.
    /// </summary>
    public string Identity { get; }

    /// <summary>
    /// Gets the CLR handler type.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Gets whether this handler requires idempotency support.
    /// </summary>
    public bool RequiresIdempotency { get; }

    /// <summary>
    /// Resolves the deterministic subscription name for one application.
    /// </summary>
    /// <param name="applicationName">The logical application name.</param>
    /// <returns>The subscription name.</returns>
    public string GetSubscriptionName(string applicationName)
    {
        return MessageTopologyNames.Subscription(applicationName, Identity);
    }
}
