using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.Diagnostics;
using LayerZero.Messaging.Dispatching;
using LayerZero.Messaging.Internal;
using LayerZero.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LayerZero.Messaging;

/// <summary>
/// Registers LayerZero async messaging services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the LayerZero messaging foundation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional messaging configuration.</param>
    /// <returns>A messaging builder.</returns>
    public static MessagingBuilder AddMessaging(
        this IServiceCollection services,
        Action<MessagingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<MessagingOptions>()
            .Validate(static options => options.MessageRoutes.Keys.All(static key => !string.IsNullOrWhiteSpace(key)),
                "Message route keys must not be empty.")
            .Validate(static options => options.MessageRoutes.Values.All(static value => !string.IsNullOrWhiteSpace(value)),
                "Message routes must target a named bus.")
            .ValidateOnStart();
        services.AddOptions<MessageConventionOptions>()
            .Validate(static options => options.BusRoutes.Keys.All(static key => !string.IsNullOrWhiteSpace(key)),
                "Message convention route keys must not be empty.")
            .Validate(static options => options.BusRoutes.Values.All(static value => !string.IsNullOrWhiteSpace(value)),
                "Message convention routes must target a named bus.")
            .Validate(static options => options.EntityNames.Keys.All(static key => !string.IsNullOrWhiteSpace(key)),
                "Message convention entity keys must not be empty.")
            .Validate(static options => options.EntityNames.Values.All(static value => !string.IsNullOrWhiteSpace(value)),
                "Message convention entity names must not be empty.")
            .ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMessageContextAccessor, AmbientMessageContextAccessor>();
        services.TryAddSingleton<IMessageConventions, DefaultMessageConventions>();
        services.TryAddSingleton<MessageEnvelopeSerializer>();
        services.TryAddSingleton<MessageRouteResolver>();
        services.TryAddSingleton<IMessageRouteResolver>(static services => services.GetRequiredService<MessageRouteResolver>());
        services.TryAddSingleton<IMessageFailureClassifier, DefaultMessageFailureClassifier>();
        services.TryAddScoped<ICommandSender, CommandSender>();
        services.TryAddScoped<IEventPublisher, EventPublisher>();
        services.TryAddScoped<IMessageProcessor, MessageProcessor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MessagingStartupValidationHostedService>());
        services.TryAddSingleton(MessagingTelemetry.Instance);

        return new MessagingBuilder(services);
    }

    /// <summary>
    /// Adds a transport topology manager.
    /// </summary>
    /// <typeparam name="TManager">The manager type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMessageTopologyManager<TManager>(this IServiceCollection services)
        where TManager : class, IMessageTopologyManager
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageTopologyManager, TManager>());
        return services;
    }
}
