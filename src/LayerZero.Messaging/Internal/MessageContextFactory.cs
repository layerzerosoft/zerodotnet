using System.Diagnostics;

namespace LayerZero.Messaging.Internal;

internal static class MessageContextFactory
{
    public static MessageContext Create(
        MessageDescriptor descriptor,
        string transportName,
        MessageContext? current,
        DateTimeOffset timestamp,
        Activity? activity)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

        var messageId = Guid.NewGuid().ToString("N");
        var correlationId = current?.CorrelationId ?? current?.MessageId ?? messageId;
        var causationId = current?.MessageId;

        return new MessageContext(
            messageId,
            descriptor.Name,
            descriptor.Kind,
            transportName,
            correlationId,
            causationId,
            activity?.Id,
            activity?.TraceStateString,
            timestamp,
            0);
    }
}
