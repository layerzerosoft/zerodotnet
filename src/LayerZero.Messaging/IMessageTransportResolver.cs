namespace LayerZero.Messaging;

/// <summary>
/// Resolves configured message transports without leaking keyed DI mechanics.
/// </summary>
public interface IMessageTransportResolver
{
    /// <summary>
    /// Resolves the transport responsible for one message descriptor.
    /// </summary>
    /// <param name="descriptor">The message descriptor.</param>
    /// <returns>The resolved transport.</returns>
    IMessageBusTransport Resolve(MessageDescriptor descriptor);

    /// <summary>
    /// Resolves one transport by its logical bus name.
    /// </summary>
    /// <param name="busName">The logical bus name.</param>
    /// <returns>The resolved transport.</returns>
    IMessageBusTransport Resolve(string busName);
}
