using LayerZero.Core;

namespace LayerZero.Messaging;

/// <summary>
/// Represents the settlement outcome of processing one incoming message.
/// </summary>
public sealed class MessageProcessingResult
{
    private MessageProcessingResult(
        MessageProcessingAction action,
        MessageContext context,
        IReadOnlyList<Error>? errors,
        string? reason)
    {
        Action = action;
        Context = context;
        Errors = errors ?? [];
        Reason = reason;
    }

    /// <summary>
    /// Gets the settlement action.
    /// </summary>
    public MessageProcessingAction Action { get; }

    /// <summary>
    /// Gets the processed message context.
    /// </summary>
    public MessageContext Context { get; }

    /// <summary>
    /// Gets any associated errors.
    /// </summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>
    /// Gets the optional human-readable reason.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Creates a successful completion result.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <returns>The processing result.</returns>
    public static MessageProcessingResult Complete(MessageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MessageProcessingResult(MessageProcessingAction.Complete, context, null, null);
    }

    /// <summary>
    /// Creates a retry result.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="reason">The retry reason.</param>
    /// <returns>The processing result.</returns>
    public static MessageProcessingResult Retry(MessageContext context, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MessageProcessingResult(MessageProcessingAction.Retry, context, null, reason);
    }

    /// <summary>
    /// Creates a dead-letter result.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="errors">The associated errors.</param>
    /// <param name="reason">The dead-letter reason.</param>
    /// <returns>The processing result.</returns>
    public static MessageProcessingResult DeadLetter(
        MessageContext context,
        IReadOnlyList<Error>? errors = null,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MessageProcessingResult(MessageProcessingAction.DeadLetter, context, errors, reason);
    }
}
