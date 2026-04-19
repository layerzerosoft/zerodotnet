using LayerZero.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LayerZero.Fulfillment.Shared;

internal static class FulfillmentProvisioning
{
    public static Task ProvisionTopologyAsync(
        Action<IServiceCollection, IConfiguration> configure,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        return ProvisionTopologyAsync(configure, configuration, "topology provisioning", cancellationToken);
    }

    public static Task ProvisionTopologyAsync(
        Action<IServiceCollection, IConfiguration> configure,
        IConfiguration configuration,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return ExecuteAsync(
            configure,
            configuration,
            operationName,
            static (services, logger, broker, ct) => ProvisionTopologyAsync(services, logger, broker, ct),
            cancellationToken);
    }

    public static Task ProvisionTopologyAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        return ProvisionTopologyAsync(services, logger: null, broker: null, cancellationToken);
    }

    private static async Task ProvisionTopologyAsync(
        IServiceProvider services,
        ILogger? logger,
        string? broker,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var managers = scope.ServiceProvider.GetServices<IMessageTopologyManager>().ToArray();
        if (managers.Length == 0)
        {
            logger?.LogInformation("No topology managers were registered for broker {Broker}.", broker);
            return;
        }

        foreach (var manager in managers)
        {
            var managerType = manager.GetType().FullName ?? manager.GetType().Name;
            logger?.LogInformation("Provisioning topology manager {TopologyManagerType} for broker {Broker}.", managerType, broker);
            await manager.ProvisionAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogInformation("Provisioned topology manager {TopologyManagerType} for broker {Broker}.", managerType, broker);
        }
    }

    private static async Task ExecuteAsync(
        Action<IServiceCollection, IConfiguration> configure,
        IConfiguration configuration,
        string operationName,
        Func<IServiceProvider, ILogger, string, CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        configure(services, configuration);
        EnsureProvisioningLogging(services);

        await using var provider = services.BuildServiceProvider();
        var broker = configuration["Messaging:Broker"] ?? "RabbitMq";
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("LayerZero.Fulfillment.Bootstrap");

        logger.LogInformation("Fulfillment provisioning operation {Operation} started for broker {Broker}.", operationName, broker);

        try
        {
            await execute(provider, logger, broker, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Fulfillment provisioning operation {Operation} completed for broker {Broker}.", operationName, broker);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Fulfillment provisioning operation {Operation} failed for broker {Broker}.", operationName, broker);
            throw;
        }
    }

    private static void EnsureProvisioningLogging(IServiceCollection services)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(ILoggerProvider)))
        {
            return;
        }

        services.AddLogging(static logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddSimpleConsole(static options => options.SingleLine = true);
        });
    }
}
