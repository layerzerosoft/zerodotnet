using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Messaging.Internal;

internal sealed class KeyedMessageTransportResolver(
    IServiceProvider services,
    IMessageRouteResolver routeResolver) : IMessageTransportResolver
{
    public IMessageBusTransport Resolve(MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return Resolve(routeResolver.Resolve(descriptor));
    }

    public IMessageBusTransport Resolve(string busName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(busName);
        return services.GetRequiredKeyedService<IMessageBusTransport>(busName);
    }
}
