using System.Security.Cryptography;
using System.Text;

namespace LayerZero.Messaging.AzureServiceBus;

internal static class AzureServiceBusNameFormatter
{
    private const int MaxEntityNameLength = 260;
    private const int MaxSubscriptionNameLength = 50;

    public static string FormatEntityName(string entityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        return Compact(entityName, MaxEntityNameLength);
    }

    public static string FormatSubscriptionName(string applicationName, string handlerIdentity)
    {
        return Compact(MessageTopologyNames.Subscription(applicationName, handlerIdentity), MaxSubscriptionNameLength);
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
        return $"{value[..prefixLength]}.{hash}".Trim('.');
    }
}
