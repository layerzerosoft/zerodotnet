using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.Kafka.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Kafka;

/// <summary>
/// Registers Kafka messaging support.
/// </summary>
public static class KafkaServiceCollectionExtensions
{
    /// <summary>
    /// Adds one named Kafka bus.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="configure">The bus configuration delegate.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddKafkaBus(
        this MessagingBuilder builder,
        string name,
        Action<KafkaBusOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<KafkaBusOptions>(name)
            .Configure(configure)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.BootstrapServers),
                "Kafka bootstrap servers must not be empty.")
            .Validate(static options => options.PollInterval > TimeSpan.Zero,
                "PollInterval must be greater than zero.")
            .Validate(static options => options.MaxDeliveryAttempts > 0,
                "MaxDeliveryAttempts must be greater than zero.")
            .ValidateOnStart();

        builder.Services.AddSingleton(new MessageBusRegistration(name, typeof(KafkaMessageBusTransport)));

        builder.Services.AddKeyedSingleton<KafkaClientProvider>(name, static (services, key) =>
            new KafkaClientProvider(
                (string)key!,
                services.GetRequiredService<IOptionsMonitor<KafkaBusOptions>>(),
                services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KafkaClientProvider>>()));

        builder.Services.AddKeyedSingleton<IMessageBusTransport>(name, static (services, key) =>
            new KafkaMessageBusTransport(
                (string)key!,
                services.GetRequiredKeyedService<KafkaClientProvider>(key!),
                services.GetRequiredService<IMessageConventions>()));

        builder.Services.AddSingleton<IMessageTopologyManager>(services =>
            new KafkaTopologyManager(
                name,
                services.GetRequiredKeyedService<KafkaClientProvider>(name),
                services.GetRequiredService<IMessageTopologyManifest>(),
                services.GetRequiredService<IMessageRouteResolver>(),
                services.GetRequiredService<IMessageConventions>(),
                services.GetRequiredService<IOptions<MessagingOptions>>()));

        builder.Services.AddSingleton<IHostedService>(services =>
            new KafkaConsumerHostedService(
                name,
                services.GetRequiredKeyedService<KafkaClientProvider>(name),
                services.GetRequiredService<IMessageTopologyManifest>(),
                services.GetRequiredService<IMessageRouteResolver>(),
                services.GetRequiredService<IMessageConventions>(),
                services.GetRequiredService<IOptions<MessagingOptions>>(),
                services.GetRequiredService<IOptionsMonitor<KafkaBusOptions>>(),
                services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<IMessageRegistry>(),
                services.GetRequiredService<Serialization.MessageEnvelopeSerializer>(),
                services.GetServices<IMessageSettlementObserver>()));

        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                $"layerzero.messaging.kafka.{name}",
                services => new KafkaHealthCheck(name, services.GetRequiredKeyedService<KafkaClientProvider>(name)),
                HealthStatus.Unhealthy,
                ["messaging", "kafka", name]));

        return builder;
    }
}
