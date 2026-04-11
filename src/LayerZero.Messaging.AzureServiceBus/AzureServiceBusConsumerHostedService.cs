using Azure.Messaging.ServiceBus;
using LayerZero.Messaging.AzureServiceBus.Configuration;
using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.AzureServiceBus;

internal sealed class AzureServiceBusConsumerHostedService(
    string name,
    AzureServiceBusClientProvider clientProvider,
    IMessageTopologyManifest topologyManifest,
    IMessageRouteResolver routeResolver,
    IMessageConventions conventions,
    IOptions<MessagingOptions> messagingOptions,
    IOptionsMonitor<AzureServiceBusBusOptions> busOptions,
    IServiceScopeFactory scopeFactory,
    IEnumerable<IMessageSettlementObserver> observers) : IHostedService, IAsyncDisposable
{
    private readonly List<object> processors = [];
    private readonly string busName = name;
    private readonly IMessageSettlementObserver[] observers = observers.ToArray();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!busOptions.Get(busName).EnableConsumers)
        {
            return;
        }

        foreach (var binding in GetBindings())
        {
            if (binding.RequiresAffinity)
            {
                var currentProcessor = binding.SubscriptionName is null
                    ? clientProvider.GetClient().CreateSessionProcessor(binding.EntityName, CreateSessionOptions())
                    : clientProvider.GetClient().CreateSessionProcessor(binding.EntityName, binding.SubscriptionName, CreateSessionOptions());
                currentProcessor.ProcessMessageAsync += args => HandleSessionMessageAsync(binding, args);

                currentProcessor.ProcessErrorAsync += _ => Task.CompletedTask;
                processors.Add(currentProcessor);
                await currentProcessor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            {
                var currentProcessor = binding.SubscriptionName is null
                    ? clientProvider.GetClient().CreateProcessor(binding.EntityName, CreateProcessorOptions())
                    : clientProvider.GetClient().CreateProcessor(binding.EntityName, binding.SubscriptionName, CreateProcessorOptions());
                currentProcessor.ProcessMessageAsync += args => HandleMessageAsync(binding, args);

                currentProcessor.ProcessErrorAsync += _ => Task.CompletedTask;
                processors.Add(currentProcessor);
                await currentProcessor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var currentProcessor in processors)
        {
            switch (currentProcessor)
            {
                case ServiceBusProcessor messageProcessor:
                    await messageProcessor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case ServiceBusSessionProcessor sessionProcessor:
                    await sessionProcessor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var currentProcessor in processors)
        {
            switch (currentProcessor)
            {
                case ServiceBusProcessor messageProcessor:
                    await messageProcessor.DisposeAsync().ConfigureAwait(false);
                    break;
                case ServiceBusSessionProcessor sessionProcessor:
                    await sessionProcessor.DisposeAsync().ConfigureAwait(false);
                    break;
            }
        }

        processors.Clear();
    }

    private ServiceBusProcessorOptions CreateProcessorOptions()
    {
        var options = busOptions.Get(busName);
        return new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            PrefetchCount = options.PrefetchCount,
            MaxConcurrentCalls = options.MaxConcurrentCalls,
            MaxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration,
        };
    }

    private ServiceBusSessionProcessorOptions CreateSessionOptions()
    {
        var options = busOptions.Get(busName);
        return new ServiceBusSessionProcessorOptions
        {
            AutoCompleteMessages = false,
            PrefetchCount = options.PrefetchCount,
            MaxConcurrentSessions = options.MaxConcurrentCalls,
            MaxConcurrentCallsPerSession = 1,
            MaxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration,
        };
    }

    private async Task HandleMessageAsync(AzureServiceBusBinding binding, ProcessMessageEventArgs args)
    {
        try
        {
            var settled = await ProcessAsync(
                binding.HandlerIdentity,
                args.Message.Body.ToMemory(),
                Math.Max(0, args.Message.DeliveryCount - 1),
                args.CancellationToken).ConfigureAwait(false);
            await SettleAsync(args, settled).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            await TryAbandonAsync(args).ConfigureAwait(false);
        }
    }

    private async Task HandleSessionMessageAsync(AzureServiceBusBinding binding, ProcessSessionMessageEventArgs args)
    {
        try
        {
            var settled = await ProcessAsync(
                binding.HandlerIdentity,
                args.Message.Body.ToMemory(),
                Math.Max(0, args.Message.DeliveryCount - 1),
                args.CancellationToken).ConfigureAwait(false);
            await SettleAsync(args, settled).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            await TryAbandonAsync(args).ConfigureAwait(false);
        }
    }

    private async Task<MessageProcessingResult> ProcessAsync(
        string handlerIdentity,
        ReadOnlyMemory<byte> body,
        int attempt,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();
        var result = await processor.ProcessAsync(
            body,
            busName,
            handlerIdentity,
            attempt: attempt,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var settled = ApplyRetryBudget(result, busOptions.Get(busName).MaxDeliveryCount);
        await NotifyAsync(settled, handlerIdentity, body, cancellationToken).ConfigureAwait(false);
        return settled;
    }

    private static Task TryAbandonAsync(ProcessMessageEventArgs args)
    {
        return args.AbandonMessageAsync(args.Message, cancellationToken: CancellationToken.None);
    }

    private static Task TryAbandonAsync(ProcessSessionMessageEventArgs args)
    {
        return args.AbandonMessageAsync(args.Message, cancellationToken: CancellationToken.None);
    }

    private static Task SettleAsync(ProcessMessageEventArgs args, MessageProcessingResult settled)
    {
        return settled.Action switch
        {
            MessageProcessingAction.Complete => args.CompleteMessageAsync(args.Message, args.CancellationToken),
            MessageProcessingAction.Retry => args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken),
            MessageProcessingAction.DeadLetter => args.DeadLetterMessageAsync(
                args.Message,
                deadLetterReason: settled.Reason ?? "LayerZero dead-lettered the message.",
                deadLetterErrorDescription: string.Join("; ", settled.Errors.Select(static error => error.Code)),
                cancellationToken: args.CancellationToken),
            _ => Task.CompletedTask,
        };
    }

    private static Task SettleAsync(ProcessSessionMessageEventArgs args, MessageProcessingResult settled)
    {
        return settled.Action switch
        {
            MessageProcessingAction.Complete => args.CompleteMessageAsync(args.Message, args.CancellationToken),
            MessageProcessingAction.Retry => args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken),
            MessageProcessingAction.DeadLetter => args.DeadLetterMessageAsync(
                args.Message,
                deadLetterReason: settled.Reason ?? "LayerZero dead-lettered the message.",
                deadLetterErrorDescription: string.Join("; ", settled.Errors.Select(static error => error.Code)),
                cancellationToken: args.CancellationToken),
            _ => Task.CompletedTask,
        };
    }

    private IEnumerable<AzureServiceBusBinding> GetBindings()
    {
        var applicationName = messagingOptions.Value.ApplicationName
            ?? throw new InvalidOperationException($"Azure Service Bus bus '{busName}' requires MessagingOptions.ApplicationName when consumers are enabled.");

        return topologyManifest.Messages
            .Where(topology => topology.Subscriptions.Count > 0)
            .Where(topology => string.Equals(routeResolver.Resolve(topology.Message), busName, StringComparison.Ordinal))
            .SelectMany(topology => topology.Subscriptions.Select(subscription =>
            {
                var entityName = AzureServiceBusNameFormatter.FormatEntityName(conventions.GetEntityName(topology.Message));
                return topology.Message.Kind == MessageKind.Command
                    ? new AzureServiceBusBinding(
                        entityName,
                        null,
                        subscription.Identity,
                        conventions.UsesAffinity(topology.Message))
                    : new AzureServiceBusBinding(
                        entityName,
                        AzureServiceBusNameFormatter.FormatSubscriptionName(applicationName, subscription.Identity),
                        subscription.Identity,
                        conventions.UsesAffinity(topology.Message));
            }))
            .ToArray();
    }

    private async Task NotifyAsync(MessageProcessingResult result, string handlerIdentity, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            await observer.OnSettledAsync(
                result.Context,
                result.Action,
                busName,
                handlerIdentity,
                result.Errors,
                result.Reason,
                body,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static MessageProcessingResult ApplyRetryBudget(MessageProcessingResult result, int maxDeliveryCount)
    {
        if (result.Action != MessageProcessingAction.Retry)
        {
            return result;
        }

        return result.Context.Attempt + 1 >= maxDeliveryCount
            ? MessageProcessingResult.DeadLetter(
                result.Context,
                result.Errors,
                BuildRetryBudgetReason(result.Reason, maxDeliveryCount))
            : result;
    }

    private static string BuildRetryBudgetReason(string? reason, int maxDeliveryCount)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? $"Retry budget exhausted after {maxDeliveryCount} delivery attempts."
            : $"{reason} Retry budget exhausted after {maxDeliveryCount} delivery attempts.";
    }

    private sealed record AzureServiceBusBinding(
        string EntityName,
        string? SubscriptionName,
        string HandlerIdentity,
        bool RequiresAffinity);
}
