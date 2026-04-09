namespace LayerZero.Core;

/// <summary>
/// Handles an asynchronous event message.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="message">The event message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    ValueTask<Result> HandleAsync(TEvent message, CancellationToken cancellationToken = default);
}
