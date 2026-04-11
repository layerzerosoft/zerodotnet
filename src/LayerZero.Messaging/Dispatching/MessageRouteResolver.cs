using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Dispatching;

internal sealed class MessageRouteResolver(
    IOptions<MessagingOptions> options,
    IMessageConventions conventions,
    IEnumerable<MessageBusRegistration> registrations) : IMessageRouteResolver
{
    private readonly MessagingOptions options = options.Value;
    private readonly MessageBusRegistration[] registrations = registrations.ToArray();

    public string Resolve(MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var conventionRoute = conventions.GetBusRoute(descriptor);
        if (!string.IsNullOrWhiteSpace(conventionRoute))
        {
            return conventionRoute;
        }

        if (options.MessageRoutes.TryGetValue(descriptor.Name, out var explicitRoute))
        {
            return explicitRoute;
        }

        if (registrations.Length == 1)
        {
            return registrations[0].Name;
        }

        throw new InvalidOperationException(
            $"Message '{descriptor.Name}' is not routed. Configure a route or register exactly one transport.");
    }
}
