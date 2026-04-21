namespace LayerZero.Messaging.Operations;

/// <summary>
/// Represents one archived dead-letter record.
/// </summary>
public sealed record DeadLetterEntry(
    string MessageId,
    string MessageName,
    string HandlerIdentity,
    string TransportName,
    string EntityName,
    int Attempt,
    string? CorrelationId,
    string? TraceParent,
    string Reason,
    string Errors,
    DateTimeOffset FailedAtUtc,
    bool Requeued);
