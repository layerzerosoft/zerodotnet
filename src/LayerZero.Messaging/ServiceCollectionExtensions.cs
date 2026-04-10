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

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMessageContextAccessor, AmbientMessageContextAccessor>();
        services.TryAddSingleton<MessageEnvelopeSerializer>();
        services.TryAddSingleton<MessageRouteResolver>();
        services.TryAddSingleton<IMessageFailureClassifier, DefaultMessageFailureClassifier>();
        services.TryAddScoped<ICommandSender, CommandSender>();
        services.TryAddScoped<IEventPublisher, EventPublisher>();
        services.TryAddScoped<IMessageProcessor, MessageProcessor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MessagingStartupValidationHostedService>());
        services.TryAddSingleton(MessagingTelemetry.Instance);

        return new MessagingBuilder(services);
    }

    /// <summary>
    /// Adds a transport topology validator.
    /// </summary>
    /// <typeparam name="TValidator">The validator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMessageTopologyValidator<TValidator>(this IServiceCollection services)
        where TValidator : class, IMessageBusTopologyValidator
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageBusTopologyValidator, TValidator>());
        return services;
    }
}
