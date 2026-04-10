namespace LayerZero.Messaging;

/// <summary>
/// Exposes the current ambient message context.
/// </summary>
public interface IMessageContextAccessor
{
    /// <summary>
    /// Gets the current message context.
    /// </summary>
    MessageContext? Current { get; }
}
