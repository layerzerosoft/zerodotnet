using System.Diagnostics;

namespace LayerZero.Messaging.Internal;

internal static class MessageContextFactory
{
    public static MessageContext Create(
        MessageDescriptor descriptor,
        object message,
        string transportName,
        MessageContext? current,
        IMessageConventions conventions,
        DateTimeOffset timestamp,
        Activity? activity)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
        ArgumentNullException.ThrowIfNull(conventions);

        var messageId = Guid.NewGuid().ToString("N");
        var correlationId = current?.CorrelationId ?? current?.MessageId ?? messageId;
        var causationId = current?.MessageId;
        var affinityKey = conventions.GetAffinityKey(descriptor, message, current);
        var traceParent = activity?.Id ?? current?.TraceParent;
        var traceState = activity?.TraceStateString ?? current?.TraceState;

        return new MessageContext(
            messageId,
            descriptor.Name,
            descriptor.Kind,
            transportName,
            correlationId,
            causationId,
            traceParent,
            traceState,
            timestamp,
            0,
            affinityKey);
    }
}
