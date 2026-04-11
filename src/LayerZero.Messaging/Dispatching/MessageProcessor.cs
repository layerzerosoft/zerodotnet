using System.Diagnostics;
using LayerZero.Core;
using LayerZero.Messaging.Diagnostics;
using LayerZero.Messaging.Internal;
using LayerZero.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Messaging.Dispatching;

internal sealed class MessageProcessor(
    IMessageRegistry registry,
    IEnumerable<IMessageHandlerInvoker> invokers,
    IServiceScopeFactory scopeFactory,
    IMessageFailureClassifier classifier,
    MessagingTelemetry telemetry,
    MessageEnvelopeSerializer serializer) : IMessageProcessor
{
    private readonly Dictionary<string, IMessageHandlerInvoker> invokersByKey = invokers
        .ToDictionary(static invoker => CreateKey(invoker.Descriptor.Name, invoker.HandlerIdentity), StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<IMessageHandlerInvoker>> invokersByMessageName = invokers
        .GroupBy(static invoker => invoker.Descriptor.Name, StringComparer.Ordinal)
        .ToDictionary(
            static group => group.Key,
            static group => (IReadOnlyList<IMessageHandlerInvoker>)group.OrderBy(static invoker => invoker.HandlerIdentity, StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);

    public async ValueTask<MessageProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> body,
        string transportName,
        string? handlerIdentity = null,
        int? attempt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

        var envelope = serializer.Deserialize(body, transportName, registry);
        if (attempt is { } attemptValue)
        {
            envelope = new DeserializedMessageEnvelope(
                envelope.Descriptor,
                envelope.Message,
                envelope.Context.WithAttempt(attemptValue));
        }

        using var activity = telemetry.ActivitySource.StartActivity("layerzero.message.process", ActivityKind.Consumer);
        activity?.SetTag("messaging.layerzero.message_name", envelope.Descriptor.Name);
        activity?.SetTag("messaging.system", transportName);
        activity?.SetTag("messaging.operation", envelope.Descriptor.Kind == MessageKind.Command ? "process" : "publish");
        if (!string.IsNullOrWhiteSpace(handlerIdentity))
        {
            activity?.SetTag("messaging.layerzero.handler", handlerIdentity);
        }

        var invoker = ResolveInvoker(envelope.Descriptor.Name, handlerIdentity);
        if (invoker is null)
        {
            telemetry.FailedCounter.Add(1);
            return MessageProcessingResult.DeadLetter(
                envelope.Context,
                [Error.Create("layerzero.messaging.handler_missing", BuildMissingInvokerMessage(envelope.Descriptor.Name, handlerIdentity))]);
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var accessor = (AmbientMessageContextAccessor)scope.ServiceProvider.GetRequiredService<IMessageContextAccessor>();

            using var messageScope = accessor.Push(envelope.Context);
            var handling = await invoker
                .InvokeAsync(scope.ServiceProvider, envelope.Message, envelope.Context, cancellationToken)
                .ConfigureAwait(false);

            if (handling.IsSuccess)
            {
                telemetry.ProcessedCounter.Add(1);
                return MessageProcessingResult.Complete(envelope.Context);
            }

            if (handling.IsValidationFailure)
            {
                telemetry.FailedCounter.Add(1);
                var action = classifier.ClassifyValidationFailure(envelope.Context, handling.Validation!);
                return action == MessageFailureAction.Retry
                    ? MessageProcessingResult.Retry(envelope.Context, "Validation failure was classified as retryable.")
                    : MessageProcessingResult.DeadLetter(envelope.Context, handling.Validation!.Errors.Select(static error => error.ToError()).ToArray());
            }

            if (handling.IsHandlerFailure)
            {
                telemetry.FailedCounter.Add(1);
                var action = classifier.ClassifyResultFailure(envelope.Context, handling.Result!);
                return action == MessageFailureAction.Retry
                    ? MessageProcessingResult.Retry(envelope.Context, "Handler failure was classified as retryable.")
                    : MessageProcessingResult.DeadLetter(envelope.Context, handling.Result!.Errors);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            telemetry.FailedCounter.Add(1);
            var action = classifier.ClassifyException(envelope.Context, exception);
            return action == MessageFailureAction.Retry
                ? MessageProcessingResult.Retry(envelope.Context, exception.Message)
                : MessageProcessingResult.DeadLetter(
                    envelope.Context,
                    [Error.Create("layerzero.messaging.unhandled_exception", exception.Message)]);
        }

        telemetry.ProcessedCounter.Add(1);
        return MessageProcessingResult.Complete(envelope.Context);
    }

    private IMessageHandlerInvoker? ResolveInvoker(string messageName, string? handlerIdentity)
    {
        if (!string.IsNullOrWhiteSpace(handlerIdentity))
        {
            invokersByKey.TryGetValue(CreateKey(messageName, handlerIdentity), out var resolved);
            return resolved;
        }

        if (!invokersByMessageName.TryGetValue(messageName, out var invokersForMessage))
        {
            return null;
        }

        return invokersForMessage.Count == 1 ? invokersForMessage[0] : null;
    }

    private static string BuildMissingInvokerMessage(string messageName, string? handlerIdentity)
    {
        return string.IsNullOrWhiteSpace(handlerIdentity)
            ? $"No invoker exists for '{messageName}'."
            : $"No invoker exists for '{messageName}' and handler '{handlerIdentity}'.";
    }

    private static string CreateKey(string messageName, string handlerIdentity)
    {
        return $"{messageName}::{handlerIdentity}";
    }
}
