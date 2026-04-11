using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Internal;

internal sealed class DefaultMessageConventions(IOptions<MessageConventionOptions> options) : IMessageConventions
{
    private readonly MessageConventionOptions options = options.Value;

    public string? GetBusRoute(MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return options.BusRoutes.TryGetValue(descriptor.Name, out var busRoute) ? busRoute : null;
    }

    public string GetEntityName(MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return options.EntityNames.TryGetValue(descriptor.Name, out var entityName)
            ? entityName
            : descriptor.EntityName;
    }

    public bool UsesAffinity(MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return options.TryGetAffinitySelector(descriptor.Name, out _)
            || descriptor.SupportsAffinity;
    }

    public string? GetAffinityKey(MessageDescriptor descriptor, object message, MessageContext? current)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(message);

        if (options.TryGetAffinitySelector(descriptor.Name, out var selector))
        {
            return Normalize(selector(message) ?? current?.AffinityKey);
        }

        if (descriptor.DefaultAffinityKeyAccessor is not null)
        {
            return Normalize(descriptor.DefaultAffinityKeyAccessor(message) ?? current?.AffinityKey);
        }

        return Normalize(current?.AffinityKey);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var guid)
            ? guid.ToString("N")
            : value;
    }
}
