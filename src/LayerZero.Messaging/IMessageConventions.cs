namespace LayerZero.Messaging;

/// <summary>
/// Resolves runtime messaging conventions such as routes, entity names, and affinity keys.
/// </summary>
public interface IMessageConventions
{
    /// <summary>
    /// Gets the explicit bus route override for a message when configured.
    /// </summary>
    /// <param name="descriptor">The message descriptor.</param>
    /// <returns>The bus route override, or <see langword="null"/>.</returns>
    string? GetBusRoute(MessageDescriptor descriptor);

    /// <summary>
    /// Resolves the entity name for a message.
    /// </summary>
    /// <param name="descriptor">The message descriptor.</param>
    /// <returns>The entity name.</returns>
    string GetEntityName(MessageDescriptor descriptor);

    /// <summary>
    /// Gets whether a message should be treated as affinity-aware.
    /// </summary>
    /// <param name="descriptor">The message descriptor.</param>
    /// <returns><see langword="true"/> when the message uses affinity.</returns>
    bool UsesAffinity(MessageDescriptor descriptor);

    /// <summary>
    /// Resolves an affinity key for a concrete message instance.
    /// </summary>
    /// <param name="descriptor">The message descriptor.</param>
    /// <param name="message">The message instance.</param>
    /// <param name="current">The ambient message context, if one exists.</param>
    /// <returns>The resolved affinity key, or <see langword="null"/>.</returns>
    string? GetAffinityKey(MessageDescriptor descriptor, object message, MessageContext? current);
}
