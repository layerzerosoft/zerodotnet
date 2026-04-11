using System.Diagnostics;
using LayerZero.Core;
using LayerZero.Messaging.Diagnostics;
using LayerZero.Messaging.Internal;
using LayerZero.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Messaging.Dispatching;

internal sealed class EventPublisher(
    IServiceProvider services,
    IMessageRegistry registry,
    MessageRouteResolver routeResolver,
    MessageEnvelopeSerializer serializer,
    IMessageContextAccessor contextAccessor,
    IMessageConventions conventions,
    TimeProvider timeProvider,
    MessagingTelemetry telemetry) : IEventPublisher
{
    public async ValueTask<Result> PublishAsync<TEvent>(TEvent eventMessage, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(eventMessage);

        if (!registry.TryGetDescriptor(typeof(TEvent), out var descriptor))
        {
            return Result.Failure(Error.Create(
                "layerzero.messaging.message_not_registered",
                $"Message '{typeof(TEvent).FullName}' is not registered for messaging."));
        }

        if (descriptor.Kind != MessageKind.Event)
        {
            return Result.Failure(Error.Create(
                "layerzero.messaging.invalid_message_kind",
                $"Message '{descriptor.Name}' is not an event."));
        }

        var busName = routeResolver.Resolve(descriptor);
        var transport = services.GetRequiredKeyedService<IMessageBusTransport>(busName);
        var context = MessageContextFactory.Create(
            descriptor,
            eventMessage,
            busName,
            contextAccessor.Current,
            conventions,
            timeProvider.GetUtcNow(),
            Activity.Current);

        using var activity = telemetry.ActivitySource.StartActivity("layerzero.event.publish", ActivityKind.Producer);
        activity?.SetTag("messaging.layerzero.message_name", descriptor.Name);
        activity?.SetTag("messaging.system", busName);

        await transport
            .PublishAsync(new TransportMessage(descriptor, context, serializer.Serialize(descriptor, eventMessage, context)), cancellationToken)
            .ConfigureAwait(false);

        telemetry.PublishedCounter.Add(1);
        return Result.Success();
    }
}
