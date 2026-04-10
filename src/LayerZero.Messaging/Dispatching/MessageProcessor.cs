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
    private readonly Dictionary<string, IMessageHandlerInvoker> invokersByName = invokers
        .ToDictionary(invoker => invoker.Descriptor.Name, StringComparer.Ordinal);

    public async ValueTask<MessageProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> body,
        string transportName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

        var envelope = serializer.Deserialize(body, transportName, registry);
        using var activity = telemetry.ActivitySource.StartActivity("layerzero.message.process", ActivityKind.Consumer);
        activity?.SetTag("messaging.layerzero.message_name", envelope.Descriptor.Name);
        activity?.SetTag("messaging.system", transportName);
        activity?.SetTag("messaging.operation", envelope.Descriptor.Kind == MessageKind.Command ? "process" : "publish");

        if (!invokersByName.TryGetValue(envelope.Descriptor.Name, out var invoker))
        {
            telemetry.FailedCounter.Add(1);
            return MessageProcessingResult.DeadLetter(
                envelope.Context,
                [Error.Create("layerzero.messaging.handler_missing", $"No invoker exists for '{envelope.Descriptor.Name}'.")]);
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
}
