namespace LayerZero.Messaging;

/// <summary>
/// Describes standard LayerZero message metadata.
/// </summary>
public sealed class MessageContext
{
    /// <summary>
    /// Initializes a new <see cref="MessageContext"/>.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="messageName">The logical LayerZero message name.</param>
    /// <param name="messageKind">The message kind.</param>
    /// <param name="transportName">The transport name.</param>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="causationId">The causation identifier.</param>
    /// <param name="traceParent">The W3C traceparent value.</param>
    /// <param name="traceState">The W3C tracestate value.</param>
    /// <param name="timestamp">The message timestamp.</param>
    /// <param name="attempt">The delivery attempt count.</param>
    /// <param name="headers">Additional arbitrary headers.</param>
    public MessageContext(
        string messageId,
        string messageName,
        MessageKind messageKind,
        string transportName,
        string? correlationId,
        string? causationId,
        string? traceParent,
        string? traceState,
        DateTimeOffset timestamp,
        int attempt,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

        MessageId = messageId;
        MessageName = messageName;
        MessageKind = messageKind;
        TransportName = transportName;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
        CausationId = string.IsNullOrWhiteSpace(causationId) ? null : causationId;
        TraceParent = string.IsNullOrWhiteSpace(traceParent) ? null : traceParent;
        TraceState = string.IsNullOrWhiteSpace(traceState) ? null : traceState;
        Timestamp = timestamp;
        Attempt = attempt;
        Headers = headers is null
            ? EmptyHeaders
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the unique message identifier.
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// Gets the logical message name.
    /// </summary>
    public string MessageName { get; }

    /// <summary>
    /// Gets the message kind.
    /// </summary>
    public MessageKind MessageKind { get; }

    /// <summary>
    /// Gets the logical transport name.
    /// </summary>
    public string TransportName { get; }

    /// <summary>
    /// Gets the correlation identifier.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Gets the causation identifier.
    /// </summary>
    public string? CausationId { get; }

    /// <summary>
    /// Gets the traceparent value.
    /// </summary>
    public string? TraceParent { get; }

    /// <summary>
    /// Gets the tracestate value.
    /// </summary>
    public string? TraceState { get; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the current delivery attempt.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// Gets arbitrary additional headers.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Creates a copy with a different transport.
    /// </summary>
    /// <param name="transportName">The new transport name.</param>
    /// <returns>The copied context.</returns>
    public MessageContext WithTransport(string transportName)
    {
        return new MessageContext(
            MessageId,
            MessageName,
            MessageKind,
            transportName,
            CorrelationId,
            CausationId,
            TraceParent,
            TraceState,
            Timestamp,
            Attempt,
            Headers);
    }

    /// <summary>
    /// Creates a copy with a different attempt count.
    /// </summary>
    /// <param name="attempt">The new attempt count.</param>
    /// <returns>The copied context.</returns>
    public MessageContext WithAttempt(int attempt)
    {
        return new MessageContext(
            MessageId,
            MessageName,
            MessageKind,
            TransportName,
            CorrelationId,
            CausationId,
            TraceParent,
            TraceState,
            Timestamp,
            attempt,
            Headers);
    }
}
