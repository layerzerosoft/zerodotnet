namespace LayerZero.Messaging;

/// <summary>
/// Selects which runtime messaging services should be registered for one transport.
/// </summary>
public enum MessageTransportRole
{
    /// <summary>
    /// Registers send and publish services only.
    /// </summary>
    SendOnly = 0,

    /// <summary>
    /// Registers send/publish services, topology management, and consumers.
    /// </summary>
    Consumers = 1,

    /// <summary>
    /// Registers topology management services only.
    /// </summary>
    Administration = 2,
}
