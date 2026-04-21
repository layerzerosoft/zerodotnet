using LayerZero.Messaging.Operations.Configuration;
using LayerZero.Messaging.Operations.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Messaging.Operations;

/// <summary>
/// Registers LayerZero messaging operations services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds LayerZero messaging operations services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The optional configuration delegate.</param>
    /// <returns>The configured builder.</returns>
    public static MessagingOperationsBuilder AddMessagingOperations(
        this IServiceCollection services,
        Action<MessagingOperationsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<MessagingOperationsOptions>().ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton<IDeadLetterReplayService, DeadLetterReplayService>();
        return new MessagingOperationsBuilder(services);
    }
}
