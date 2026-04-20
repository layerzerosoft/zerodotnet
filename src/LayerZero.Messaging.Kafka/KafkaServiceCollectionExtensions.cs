using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.Kafka.Configuration;
using Microsoft.Extensions.Configuration;
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
    /// Adds one Kafka transport from configuration.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="role">The transport role.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="sectionPath">The Kafka configuration section.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddKafka(
        this MessagingBuilder builder,
        IConfiguration configuration,
        MessageTransportRole role = MessageTransportRole.Consumers,
        string name = "primary",
        string sectionPath = "Messaging:Kafka")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return builder.AddKafkaBus(
            name,
            options =>
            {
                Bind(configuration, sectionPath, options);
                options.BootstrapServers = ResolveConnectionString(
                    configuration,
                    primaryConnectionStringName: "kafka",
                    fallbackConnectionStringName: "messaging",
                    options.BootstrapServers);
            },
            role);
    }

    /// <summary>
    /// Adds one named Kafka bus.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="configure">The bus configuration delegate.</param>
    /// <param name="role">The runtime role for the transport.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddKafkaBus(
        this MessagingBuilder builder,
        string name,
        Action<KafkaBusOptions> configure,
        MessageTransportRole role = MessageTransportRole.Consumers)
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

        if (role is MessageTransportRole.SendOnly or MessageTransportRole.Consumers)
        {
            builder.Services.AddKeyedSingleton<IMessageBusTransport>(name, static (services, key) =>
                new KafkaMessageBusTransport(
                    (string)key!,
                    services.GetRequiredKeyedService<KafkaClientProvider>(key!),
                    services.GetRequiredService<IMessageConventions>()));
        }

        if (role is MessageTransportRole.Consumers or MessageTransportRole.Administration)
        {
            builder.Services.AddSingleton<IMessageTopologyManager>(services =>
                new KafkaTopologyManager(
                    name,
                    services.GetRequiredKeyedService<KafkaClientProvider>(name),
                    services.GetRequiredService<IMessageTopologyManifest>(),
                    services.GetRequiredService<IMessageRouteResolver>(),
                    services.GetRequiredService<IMessageConventions>(),
                    services.GetRequiredService<IOptions<MessagingOptions>>()));
        }

        if (role is MessageTransportRole.Consumers)
        {
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
        }

        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                $"layerzero.messaging.kafka.{name}",
                services => new KafkaHealthCheck(name, services.GetRequiredKeyedService<KafkaClientProvider>(name)),
                HealthStatus.Unhealthy,
                ["messaging", "kafka", name]));

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
