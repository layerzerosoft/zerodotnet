namespace LayerZero.Messaging.Nats;

internal static class NatsJetStreamNames
{
    public static string Stream(string subject)
    {
        return $"LZ_{MessageTopologyNames.Normalize(subject).Replace(".", "_", StringComparison.Ordinal).ToUpperInvariant()}";
    }
}
