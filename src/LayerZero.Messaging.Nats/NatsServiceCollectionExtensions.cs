using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.Nats.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Nats;

/// <summary>
/// Registers NATS JetStream messaging support.
/// </summary>
public static class NatsServiceCollectionExtensions
{
    /// <summary>
    /// Adds one NATS transport from configuration.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="role">The transport role.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="sectionPath">The NATS configuration section.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddNats(
        this MessagingBuilder builder,
        IConfiguration configuration,
        MessageTransportRole role = MessageTransportRole.Consumers,
        string name = "primary",
        string sectionPath = "Messaging:Nats")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return builder.AddNatsBus(
            name,
            options =>
            {
                Bind(configuration, sectionPath, options);
                options.Url = ResolveConnectionString(
                    configuration,
                    primaryConnectionStringName: "nats",
                    fallbackConnectionStringName: "messaging",
                    options.Url);
            },
            role);
    }

    /// <summary>
    /// Adds one named NATS JetStream bus.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="configure">The bus configuration delegate.</param>
    /// <param name="role">The runtime role for the transport.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddNatsBus(
        this MessagingBuilder builder,
        string name,
        Action<NatsBusOptions> configure,
        MessageTransportRole role = MessageTransportRole.Consumers)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<NatsBusOptions>(name)
            .Configure(configure)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Url), "NATS URLs must not be empty.")
            .Validate(static options => options.RetryDelay > TimeSpan.Zero, "RetryDelay must be greater than zero.")
            .Validate(static options => options.MaxDeliver > 0, "MaxDeliver must be greater than zero.")
            .ValidateOnStart();

        builder.Services.AddSingleton(new MessageBusRegistration(name, typeof(NatsMessageBusTransport)));

        builder.Services.AddKeyedSingleton<NatsClientProvider>(name, static (services, key) =>
            new NatsClientProvider((string)key!, services.GetRequiredService<IOptionsMonitor<NatsBusOptions>>()));

        if (role is MessageTransportRole.SendOnly or MessageTransportRole.Consumers)
        {
            builder.Services.AddKeyedSingleton<IMessageBusTransport>(name, static (services, key) =>
                new NatsMessageBusTransport(
                    (string)key!,
                    services.GetRequiredKeyedService<NatsClientProvider>(key!),
                    services.GetRequiredService<IMessageConventions>()));
        }

        if (role is MessageTransportRole.Consumers or MessageTransportRole.Administration)
        {
            builder.Services.AddSingleton<IMessageTopologyManager>(services =>
                new NatsTopologyManager(
                    name,
                    services.GetRequiredKeyedService<NatsClientProvider>(name),
                    services.GetRequiredService<IMessageTopologyManifest>(),
                    services.GetRequiredService<IMessageRouteResolver>(),
                    services.GetRequiredService<IMessageConventions>(),
                    services.GetRequiredService<IOptions<MessagingOptions>>()));
        }

        if (role is MessageTransportRole.Consumers)
        {
            builder.Services.AddSingleton<IHostedService>(services =>
                new NatsConsumerHostedService(
                    name,
                    services.GetRequiredKeyedService<NatsClientProvider>(name),
                    services.GetRequiredService<IMessageTopologyManifest>(),
                    services.GetRequiredService<IMessageRouteResolver>(),
                    services.GetRequiredService<IMessageConventions>(),
                    services.GetRequiredService<IOptions<MessagingOptions>>(),
                    services.GetRequiredService<IOptionsMonitor<NatsBusOptions>>(),
                    services.GetRequiredService<IServiceScopeFactory>(),
                    services.GetServices<IMessageSettlementObserver>()));
        }

        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                $"layerzero.messaging.nats.{name}",
                services => new NatsHealthCheck(name, services.GetRequiredKeyedService<NatsClientProvider>(name)),
                HealthStatus.Unhealthy,
                ["messaging", "nats", name]));

        return builder;
    }

    private static void Bind<TOptions>(IConfiguration configuration, string sectionPath, TOptions options)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        ArgumentNullException.ThrowIfNull(options);

        configuration.GetSection(sectionPath).Bind(options);
    }

    private static string ResolveConnectionString(
        IConfiguration configuration,
        string primaryConnectionStringName,
        string fallbackConnectionStringName,
        string? configuredValue)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryConnectionStringName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackConnectionStringName);

        var primary = configuration.GetConnectionString(primaryConnectionStringName);
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return configuredValue;
        }

        var fallback = configuration.GetConnectionString(fallbackConnectionStringName);
        return fallback ?? string.Empty;
    }
}
