namespace LayerZero.Messaging;

/// <summary>
/// Provides access to the discovered LayerZero message manifest.
/// </summary>
public interface IMessageRegistry
{
    /// <summary>
    /// Gets all discovered messages.
    /// </summary>
    IReadOnlyList<MessageDescriptor> Messages { get; }

    /// <summary>
    /// Attempts to resolve a message descriptor by CLR type.
    /// </summary>
    /// <param name="messageType">The CLR message type.</param>
    /// <param name="descriptor">The resolved descriptor.</param>
    /// <returns><see langword="true"/> when found.</returns>
    bool TryGetDescriptor(Type messageType, out MessageDescriptor descriptor);

    /// <summary>
    /// Attempts to resolve a message descriptor by logical name.
    /// </summary>
    /// <param name="messageName">The logical message name.</param>
    /// <param name="descriptor">The resolved descriptor.</param>
    /// <returns><see langword="true"/> when found.</returns>
    bool TryGetDescriptor(string messageName, out MessageDescriptor descriptor);
}
