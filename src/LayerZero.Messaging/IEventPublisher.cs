using LayerZero.Core;

namespace LayerZero.Messaging;

/// <summary>
/// Publishes LayerZero events over a configured transport.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="eventMessage">The event instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    ValueTask<Result> PublishAsync<TEvent>(TEvent eventMessage, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}
