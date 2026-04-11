using LayerZero.Messaging.AzureServiceBus.Configuration;
using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.AzureServiceBus;

/// <summary>
/// Registers Azure Service Bus messaging support.
/// </summary>
public static class AzureServiceBusServiceCollectionExtensions
{
    /// <summary>
    /// Adds one named Azure Service Bus transport.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="configure">The bus configuration delegate.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddAzureServiceBusBus(
        this MessagingBuilder builder,
        string name,
        Action<AzureServiceBusBusOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<AzureServiceBusBusOptions>(name)
            .Configure(configure)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Azure Service Bus connection strings must not be empty.")
            .Validate(static options => options.MaxConcurrentCalls > 0,
                "MaxConcurrentCalls must be greater than zero.")
            .Validate(static options => options.MaxAutoLockRenewalDuration > TimeSpan.Zero,
                "MaxAutoLockRenewalDuration must be greater than zero.")
            .Validate(static options => options.MaxDeliveryCount > 0,
                "MaxDeliveryCount must be greater than zero.")
            .ValidateOnStart();

        builder.Services.AddSingleton(new MessageBusRegistration(name, typeof(AzureServiceBusMessageBusTransport)));

        builder.Services.AddKeyedSingleton<AzureServiceBusClientProvider>(name, static (services, key) =>
            new AzureServiceBusClientProvider((string)key!, services.GetRequiredService<IOptionsMonitor<AzureServiceBusBusOptions>>()));

        builder.Services.AddKeyedSingleton<IMessageBusTransport>(name, static (services, key) =>
            new AzureServiceBusMessageBusTransport(
                (string)key!,
                services.GetRequiredKeyedService<AzureServiceBusClientProvider>(key!),
                services.GetRequiredService<IMessageConventions>()));

        builder.Services.AddSingleton<IMessageTopologyManager>(services =>
            new AzureServiceBusTopologyManager(
                name,
                services.GetRequiredKeyedService<AzureServiceBusClientProvider>(name),
                services.GetRequiredService<IMessageTopologyManifest>(),
                services.GetRequiredService<IMessageRouteResolver>(),
                services.GetRequiredService<IMessageConventions>(),
                services.GetRequiredService<IOptions<MessagingOptions>>()));

        builder.Services.AddSingleton<IHostedService>(services =>
            new AzureServiceBusConsumerHostedService(
                name,
                services.GetRequiredKeyedService<AzureServiceBusClientProvider>(name),
                services.GetRequiredService<IMessageTopologyManifest>(),
                services.GetRequiredService<IMessageRouteResolver>(),
                services.GetRequiredService<IMessageConventions>(),
                services.GetRequiredService<IOptions<MessagingOptions>>(),
                services.GetRequiredService<IOptionsMonitor<AzureServiceBusBusOptions>>(),
                services.GetRequiredService<IServiceScopeFactory>(),
                services.GetServices<IMessageSettlementObserver>()));

        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                $"layerzero.messaging.azure-service-bus.{name}",
                services => new AzureServiceBusHealthCheck(name, services.GetRequiredKeyedService<AzureServiceBusClientProvider>(name)),
                HealthStatus.Unhealthy,
                ["messaging", "azure-service-bus", name]));

        return builder;
    }
}
