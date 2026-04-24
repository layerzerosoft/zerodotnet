using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.Diagnostics;
using LayerZero.Messaging.Dispatching;
using LayerZero.Messaging.Internal;
using LayerZero.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.CompilerServices;

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
    /// <param name="applicationName">
    /// The logical application name. This value overrides configuration and host-derived defaults.
    /// </param>
    /// <returns>A messaging builder.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static MessagingBuilder AddMessaging(
        this IServiceCollection services,
        string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        var scopeAssembly = Assembly.GetCallingAssembly();
        return AddMessagingCore(services, options => options.ApplicationName = applicationName, scopeAssembly);
    }

    /// <summary>
    /// Adds the LayerZero messaging foundation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional messaging configuration. LayerZero binds the <c>Messaging</c> configuration section first, then
    /// applies host-derived defaults, and finally applies this explicit configuration.
    /// </param>
    /// <returns>A messaging builder.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static MessagingBuilder AddMessaging(
        this IServiceCollection services,
        Action<MessagingOptions>? configure = null)
    {
        var scopeAssembly = Assembly.GetCallingAssembly();
        return AddMessagingCore(services, configure, scopeAssembly);
    }

    /// <summary>
    /// Adds the LayerZero messaging foundation using an explicit discovery scope assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="scopeAssembly">The assembly whose generated LayerZero registrations should anchor discovery.</param>
    /// <param name="configure">
    /// Optional messaging configuration. LayerZero binds the <c>Messaging</c> configuration section first, then
    /// applies host-derived defaults, and finally applies this explicit configuration.
    /// </param>
    /// <returns>A messaging builder.</returns>
    public static MessagingBuilder AddMessaging(
        this IServiceCollection services,
        Assembly scopeAssembly,
        Action<MessagingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(scopeAssembly);
        return AddMessagingCore(services, configure, scopeAssembly);
    }

    /// <summary>
    /// Adds the LayerZero messaging foundation using the assembly that contains <typeparamref name="TScopeMarker" />.
    /// </summary>
    /// <typeparam name="TScopeMarker">A marker type from the desired discovery scope assembly.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional messaging configuration. LayerZero binds the <c>Messaging</c> configuration section first, then
    /// applies host-derived defaults, and finally applies this explicit configuration.
    /// </param>
    /// <returns>A messaging builder.</returns>
    public static MessagingBuilder AddMessaging<TScopeMarker>(
        this IServiceCollection services,
        Action<MessagingOptions>? configure = null)
    {
        return AddMessaging(services, typeof(TScopeMarker).Assembly, configure);
    }

    private static MessagingBuilder AddMessagingCore(
        IServiceCollection services,
        Action<MessagingOptions>? configure,
        Assembly? scopeAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<MessagingOptions>, MessagingOptionsSetup>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<MessagingOptions>, MessagingOptionsSetup>());
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
        services.TryAddSingleton<IMessageTransportResolver, KeyedMessageTransportResolver>();
        services.TryAddSingleton<IMessageFailureClassifier, DefaultMessageFailureClassifier>();
        services.TryAddSingleton<IMessageTopologyProvisioner, MessageTopologyProvisioner>();
        services.TryAddScoped<ICommandSender, CommandSender>();
        services.TryAddScoped<IEventPublisher, EventPublisher>();
        services.TryAddScoped<IMessageProcessor, MessageProcessor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MessagingStartupValidationHostedService>());
        services.TryAddSingleton(MessagingTelemetry.Instance);

        MessagingAssemblyRegistrarCatalog.Apply(services, scopeAssembly);

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
