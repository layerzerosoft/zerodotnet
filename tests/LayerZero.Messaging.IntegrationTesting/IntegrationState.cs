using System.Collections.Concurrent;

namespace LayerZero.Messaging.IntegrationTesting;

public sealed class IntegrationState
{
    private readonly ConcurrentDictionary<string, int> counters = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ObservedInvocation> invocations = new();
    private readonly ConcurrentQueue<ObservedSettlement> settlements = new();

    public IReadOnlyList<ObservedInvocation> Invocations => invocations.ToArray();

    public IReadOnlyList<ObservedSettlement> Settlements => settlements.ToArray();

    public int Increment(string key)
    {
        return counters.AddOrUpdate(key, 1, static (_, current) => current + 1);
    }

    public int Count(string key)
    {
        return counters.TryGetValue(key, out var count) ? count : 0;
    }

    public void RecordInvocation(string marker, MessageContext context, string handlerIdentity)
    {
        Increment(marker);
        invocations.Enqueue(new ObservedInvocation(
            marker,
            context.MessageName,
            handlerIdentity,
            context.MessageId,
            context.CorrelationId,
            context.TraceParent,
            context.Attempt,
            context.TransportName,
            context.AffinityKey,
            DateTimeOffset.UtcNow));
    }

    public void RecordSettlement(
        MessageContext context,
        MessageProcessingAction action,
        string transportName,
        string? handlerIdentity,
        IReadOnlyList<LayerZero.Core.Error> errors,
        string? reason)
    {
        settlements.Enqueue(new ObservedSettlement(
            context.MessageName,
            action,
            transportName,
            handlerIdentity,
            context.MessageId,
            context.CorrelationId,
            context.TraceParent,
            context.Attempt,
            errors.Select(static error => error.Code).ToArray(),
            reason,
            DateTimeOffset.UtcNow));
    }

    public async Task WaitForAsync(Func<IntegrationState, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (predicate(this))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for the integration scenario state to reach the expected condition.");
    }
}

public sealed record ObservedInvocation(
    string Marker,
    string MessageName,
    string HandlerIdentity,
    string MessageId,
    string? CorrelationId,
    string? TraceParent,
    int Attempt,
    string TransportName,
    string? AffinityKey,
    DateTimeOffset OccurredAtUtc);

public sealed record ObservedSettlement(
    string MessageName,
    MessageProcessingAction Action,
    string TransportName,
    string? HandlerIdentity,
    string MessageId,
    string? CorrelationId,
    string? TraceParent,
    int Attempt,
    IReadOnlyList<string> ErrorCodes,
    string? Reason,
    DateTimeOffset OccurredAtUtc);
