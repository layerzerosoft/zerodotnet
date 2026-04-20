using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.RabbitMq;

/// <summary>
/// Registers RabbitMQ messaging support.
/// </summary>
public static class RabbitMqServiceCollectionExtensions
{
    /// <summary>
    /// Adds one RabbitMQ transport from configuration.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="role">The transport role.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="sectionPath">The RabbitMQ configuration section.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddRabbitMq(
        this MessagingBuilder builder,
        IConfiguration configuration,
        MessageTransportRole role = MessageTransportRole.Consumers,
        string name = "primary",
        string sectionPath = "Messaging:RabbitMq")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return builder.AddRabbitMqBus(
            name,
            options =>
            {
                Bind(configuration, sectionPath, options);
                options.ConnectionString = ResolveConnectionString(
                    configuration,
                    primaryConnectionStringName: "rabbitmq",
                    fallbackConnectionStringName: "messaging",
                    options.ConnectionString);
            },
            role);
    }

    /// <summary>
    /// Adds one named RabbitMQ bus.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="configure">The bus configuration delegate.</param>
    /// <param name="role">The runtime role for the transport.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddRabbitMqBus(
        this MessagingBuilder builder,
        string name,
        Action<RabbitMqBusOptions> configure,
        MessageTransportRole role = MessageTransportRole.Consumers)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<RabbitMqBusOptions>(name)
            .Configure(configure)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "RabbitMQ connection strings must not be empty.")
            .Validate(static options => Uri.TryCreate(options.ConnectionString, UriKind.Absolute, out _),
                "RabbitMQ connection strings must be absolute URIs.")
            .Validate(static options => options.RetryDelay > TimeSpan.Zero, "RetryDelay must be greater than zero.")
            .Validate(static options => options.MaxDeliveryAttempts > 0, "MaxDeliveryAttempts must be greater than zero.")
            .Validate(static options => options.PublisherConfirmationTimeout > TimeSpan.Zero, "PublisherConfirmationTimeout must be greater than zero.")
            .ValidateOnStart();

        builder.Services.AddSingleton(new MessageBusRegistration(name, typeof(RabbitMqMessageBusTransport)));

        builder.Services.AddKeyedSingleton<RabbitMqConnectionProvider>(name, static (services, key) =>
            new RabbitMqConnectionProvider((string)key!, services.GetRequiredService<IOptionsMonitor<RabbitMqBusOptions>>()));

        if (role is MessageTransportRole.SendOnly or MessageTransportRole.Consumers)
        {
            builder.Services.AddKeyedSingleton<IMessageBusTransport>(name, static (services, key) =>
                new RabbitMqMessageBusTransport(
                    (string)key!,
                    services.GetRequiredKeyedService<RabbitMqConnectionProvider>(key!),
                    services.GetRequiredService<IMessageConventions>()));
        }

        if (role is MessageTransportRole.Consumers or MessageTransportRole.Administration)
        {
            builder.Services.AddSingleton<IMessageTopologyManager>(services =>
                new RabbitMqTopologyManager(
                    name,
                    services.GetRequiredKeyedService<RabbitMqConnectionProvider>(name),
                    services.GetRequiredService<IMessageTopologyManifest>(),
                    services.GetRequiredService<IMessageRouteResolver>(),
                    services.GetRequiredService<IMessageConventions>(),
                    services.GetRequiredService<IOptions<MessagingOptions>>()));
        }

        if (role is MessageTransportRole.Consumers)
        {
            builder.Services.AddSingleton<IHostedService>(services =>
                new RabbitMqConsumerHostedService(
                    name,
                    services.GetRequiredKeyedService<RabbitMqConnectionProvider>(name),
                    services.GetRequiredService<IMessageTopologyManifest>(),
                    services.GetRequiredService<IMessageRouteResolver>(),
                    services.GetRequiredService<IMessageConventions>(),
                    services.GetRequiredService<IOptions<MessagingOptions>>(),
                    services.GetRequiredService<IOptionsMonitor<RabbitMqBusOptions>>(),
                    services.GetRequiredService<IServiceScopeFactory>(),
                    services.GetRequiredService<IMessageRegistry>(),
                    services.GetRequiredService<Serialization.MessageEnvelopeSerializer>(),
                    services.GetServices<IMessageSettlementObserver>()));
        }

        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                $"layerzero.messaging.rabbitmq.{name}",
                services => new RabbitMqHealthCheck(name, services.GetRequiredKeyedService<RabbitMqConnectionProvider>(name)),
                HealthStatus.Unhealthy,
                ["messaging", "rabbitmq", name]));

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
