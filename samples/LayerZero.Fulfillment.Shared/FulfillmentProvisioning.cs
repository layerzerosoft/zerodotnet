using LayerZero.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Fulfillment.Shared;

internal static class FulfillmentProvisioning
{
    public static Task InitializeStoreAsync(
        Action<IServiceCollection, IConfiguration> configure,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(configuration);

        return ExecuteAsync(
            configure,
            configuration,
            static async (services, ct) =>
            {
                await using var scope = services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<FulfillmentStore>().InitializeAsync(ct).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public static Task InitializeStoreAndProvisionAsync(
        Action<IServiceCollection, IConfiguration> configure,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(configuration);

        return ExecuteAsync(configure, configuration, InitializeStoreAndProvisionAsync, cancellationToken);
    }

    public static async Task InitializeStoreAndProvisionAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<FulfillmentStore>().InitializeAsync(cancellationToken).ConfigureAwait(false);
        foreach (var manager in scope.ServiceProvider.GetServices<IMessageTopologyManager>())
        {
            await manager.ProvisionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ExecuteAsync(
        Action<IServiceCollection, IConfiguration> configure,
        IConfiguration configuration,
        Func<IServiceProvider, CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        configure(services, configuration);

        await using var provider = services.BuildServiceProvider();
        await execute(provider, cancellationToken).ConfigureAwait(false);
    }
}
