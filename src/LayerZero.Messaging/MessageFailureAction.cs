namespace LayerZero.Messaging;

/// <summary>
/// Describes how a failed message should be treated.
/// </summary>
public enum MessageFailureAction
{
    /// <summary>
    /// Retry the message.
    /// </summary>
    Retry = 0,

    /// <summary>
    /// Move the message to dead-letter or error routing.
    /// </summary>
    DeadLetter = 1,
}
