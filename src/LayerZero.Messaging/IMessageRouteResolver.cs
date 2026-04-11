namespace LayerZero.Messaging;

/// <summary>
/// Resolves the named bus for one message descriptor.
/// </summary>
public interface IMessageRouteResolver
{
    /// <summary>
    /// Resolves the named bus for one message descriptor.
    /// </summary>
    /// <param name="descriptor">The message descriptor.</param>
    /// <returns>The resolved bus name.</returns>
    string Resolve(MessageDescriptor descriptor);
}
