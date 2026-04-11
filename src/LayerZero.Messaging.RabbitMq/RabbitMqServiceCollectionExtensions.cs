using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.RabbitMq.Configuration;
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
    /// Adds one named RabbitMQ bus.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="configure">The bus configuration delegate.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddRabbitMqBus(
        this MessagingBuilder builder,
        string name,
        Action<RabbitMqBusOptions> configure)
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

        builder.Services.AddKeyedSingleton<IMessageBusTransport>(name, static (services, key) =>
            new RabbitMqMessageBusTransport(
                (string)key!,
                services.GetRequiredKeyedService<RabbitMqConnectionProvider>(key!),
                services.GetRequiredService<IMessageConventions>()));

        builder.Services.AddSingleton<IMessageTopologyManager>(services =>
            new RabbitMqTopologyManager(
                name,
                services.GetRequiredKeyedService<RabbitMqConnectionProvider>(name),
                services.GetRequiredService<IMessageTopologyManifest>(),
                services.GetRequiredService<IMessageRouteResolver>(),
                services.GetRequiredService<IMessageConventions>(),
                services.GetRequiredService<IOptions<MessagingOptions>>()));

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

        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                $"layerzero.messaging.rabbitmq.{name}",
                services => new RabbitMqHealthCheck(name, services.GetRequiredKeyedService<RabbitMqConnectionProvider>(name)),
                HealthStatus.Unhealthy,
                ["messaging", "rabbitmq", name]));

        return builder;
    }
}
