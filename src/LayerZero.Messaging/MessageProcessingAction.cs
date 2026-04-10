namespace LayerZero.Messaging;

/// <summary>
/// Describes the settlement action a transport should take after processing.
/// </summary>
public enum MessageProcessingAction
{
    /// <summary>
    /// Complete the message successfully.
    /// </summary>
    Complete = 0,

    /// <summary>
    /// Retry the message.
    /// </summary>
    Retry = 1,

    /// <summary>
    /// Move the message to dead-letter or error routing.
    /// </summary>
    DeadLetter = 2,
}
