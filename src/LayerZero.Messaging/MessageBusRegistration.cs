namespace LayerZero.Messaging;

/// <summary>
/// Describes one configured messaging bus.
/// </summary>
public sealed class MessageBusRegistration
{
    /// <summary>
    /// Initializes a new <see cref="MessageBusRegistration"/>.
    /// </summary>
    /// <param name="name">The logical bus name.</param>
    /// <param name="transportType">The transport type.</param>
    public MessageBusRegistration(string name, Type transportType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transportType);

        Name = name;
        TransportType = transportType;
    }

    /// <summary>
    /// Gets the logical bus name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the transport implementation type.
    /// </summary>
    public Type TransportType { get; }
}
