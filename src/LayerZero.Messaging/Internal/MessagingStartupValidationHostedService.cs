using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Internal;

internal sealed class MessagingStartupValidationHostedService(
    IEnumerable<IMessageHandlerInvoker> invokers,
    IEnumerable<IMessageBusTopologyValidator> topologyValidators,
    IOptions<MessagingOptions> options,
    IServiceProvider services) : IHostedService
{
    private readonly IMessageHandlerInvoker[] invokers = invokers.ToArray();
    private readonly IMessageBusTopologyValidator[] topologyValidators = topologyValidators.ToArray();
    private readonly MessagingOptions options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (invokers.Any(static invoker => invoker.RequiresIdempotency)
            && ServiceProviderServiceExtensions.GetService<IMessageIdempotencyStore>(services) is null)
        {
            throw new InvalidOperationException(
                "At least one messaging handler requires idempotency, but no IMessageIdempotencyStore is registered.");
        }

        if (!this.options.ValidateTopologyOnStart)
        {
            return;
        }

        foreach (var validator in topologyValidators)
        {
            await validator.ValidateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
