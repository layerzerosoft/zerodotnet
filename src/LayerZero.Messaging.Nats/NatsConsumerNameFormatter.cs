using System.Security.Cryptography;
using System.Text;

namespace LayerZero.Messaging.Nats;

internal static class NatsConsumerNameFormatter
{
    private const int MaxConsumerNameLength = 128;

    public static string Format(string applicationName, string handlerIdentity)
    {
        var name = MessageTopologyNames
            .Subscription(applicationName, handlerIdentity)
            .Replace(".", "-", StringComparison.Ordinal);

        return Compact(name, MaxConsumerNameLength);
    }

    private static string Compact(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant()[..12];
        var prefixLength = Math.Max(1, maxLength - hash.Length - 1);
        return $"{value[..prefixLength]}-{hash}";
    }
}
