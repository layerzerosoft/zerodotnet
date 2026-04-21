namespace LayerZero.Messaging.Operations;

/// <summary>
/// Represents the archived transport envelope for one dead-letter record.
/// </summary>
public sealed record DeadLetterEnvelope(
    string TransportName,
    byte[] Body);
