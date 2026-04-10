namespace LayerZero.Messaging;

/// <summary>
/// Identifies the kind of message being transported.
/// </summary>
public enum MessageKind
{
    /// <summary>
    /// A command that should be handled by one logical consumer.
    /// </summary>
    Command = 0,

    /// <summary>
    /// An event that may be handled by multiple logical consumers.
    /// </summary>
    Event = 1,
}
