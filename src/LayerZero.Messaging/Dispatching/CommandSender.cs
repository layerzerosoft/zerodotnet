using System.Diagnostics;
using LayerZero.Core;
using LayerZero.Messaging.Diagnostics;
using LayerZero.Messaging.Internal;
using LayerZero.Messaging.Serialization;

namespace LayerZero.Messaging.Dispatching;

internal sealed class CommandSender(
    IMessageRegistry registry,
    MessageRouteResolver routeResolver,
    IMessageTransportResolver transportResolver,
    MessageEnvelopeSerializer serializer,
    IMessageContextAccessor contextAccessor,
    IMessageConventions conventions,
    TimeProvider timeProvider,
    MessagingTelemetry telemetry) : ICommandSender
{
    public async ValueTask<Result> SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!registry.TryGetDescriptor(typeof(TCommand), out var descriptor))
        {
            return Result.Failure(Error.Create(
                "layerzero.messaging.message_not_registered",
                $"Message '{typeof(TCommand).FullName}' is not registered for messaging."));
        }

        if (descriptor.Kind != MessageKind.Command)
        {
            return Result.Failure(Error.Create(
                "layerzero.messaging.invalid_message_kind",
                $"Message '{descriptor.Name}' is not a command."));
        }

        var busName = routeResolver.Resolve(descriptor);
        var transport = transportResolver.Resolve(busName);
        var context = MessageContextFactory.Create(
            descriptor,
            command,
            busName,
            contextAccessor.Current,
            conventions,
            timeProvider.GetUtcNow(),
            Activity.Current);

        using var activity = telemetry.ActivitySource.StartActivity("layerzero.command.send", ActivityKind.Producer);
        activity?.SetTag("messaging.layerzero.message_name", descriptor.Name);
        activity?.SetTag("messaging.system", busName);

        await transport
            .SendAsync(new TransportMessage(descriptor, context, serializer.Serialize(descriptor, command, context)), cancellationToken)
            .ConfigureAwait(false);

        telemetry.SentCounter.Add(1);
        return Result.Success();
    }
}
