using System.Collections.Concurrent;

namespace LayerZero.Messaging.IntegrationTesting;

public sealed class InMemoryMessageIdempotencyStore : IMessageIdempotencyStore
{
    private readonly ConcurrentDictionary<string, string> states = new(StringComparer.Ordinal);

    public ValueTask<bool> TryBeginAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(states.TryAdd(deduplicationKey, "processing"));
    }

    public ValueTask CompleteAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        states[deduplicationKey] = "complete";
        return ValueTask.CompletedTask;
    }

    public ValueTask AbandonAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        states.TryRemove(deduplicationKey, out _);
        return ValueTask.CompletedTask;
    }
}
