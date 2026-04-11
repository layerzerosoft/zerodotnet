namespace LayerZero.Messaging;

/// <summary>
/// Provides access to the generated message topology manifest.
/// </summary>
public interface IMessageTopologyManifest
{
    /// <summary>
    /// Gets all discovered topology entries.
    /// </summary>
    IReadOnlyList<MessageTopologyDescriptor> Messages { get; }

    /// <summary>
    /// Attempts to resolve one topology entry by CLR type.
    /// </summary>
    /// <param name="messageType">The CLR message type.</param>
    /// <param name="descriptor">The resolved descriptor.</param>
    /// <returns><see langword="true"/> when found.</returns>
    bool TryGetDescriptor(Type messageType, out MessageTopologyDescriptor descriptor);

    /// <summary>
    /// Attempts to resolve one topology entry by logical message name.
    /// </summary>
    /// <param name="messageName">The logical message name.</param>
    /// <param name="descriptor">The resolved descriptor.</param>
    /// <returns><see langword="true"/> when found.</returns>
    bool TryGetDescriptor(string messageName, out MessageTopologyDescriptor descriptor);
}
