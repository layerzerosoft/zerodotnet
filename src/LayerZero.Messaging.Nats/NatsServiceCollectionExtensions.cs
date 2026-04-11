using LayerZero.Messaging.Configuration;
using LayerZero.Messaging.Nats.Configuration;
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
    /// Adds one named NATS JetStream bus.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="configure">The bus configuration delegate.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddNatsBus(
        this MessagingBuilder builder,
        string name,
        Action<NatsBusOptions> configure)
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

        builder.Services.AddKeyedSingleton<IMessageBusTransport>(name, static (services, key) =>
            new NatsMessageBusTransport(
                (string)key!,
                services.GetRequiredKeyedService<NatsClientProvider>(key!),
                services.GetRequiredService<IMessageConventions>()));

        builder.Services.AddSingleton<IMessageTopologyManager>(services =>
            new NatsTopologyManager(
                name,
                services.GetRequiredKeyedService<NatsClientProvider>(name),
                services.GetRequiredService<IMessageTopologyManifest>(),
                services.GetRequiredService<IMessageRouteResolver>(),
                services.GetRequiredService<IMessageConventions>(),
                services.GetRequiredService<IOptions<MessagingOptions>>()));

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

        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                $"layerzero.messaging.nats.{name}",
                services => new NatsHealthCheck(name, services.GetRequiredKeyedService<NatsClientProvider>(name)),
                HealthStatus.Unhealthy,
                ["messaging", "nats", name]));

        return builder;
    }
}
