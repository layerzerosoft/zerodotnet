using LayerZero.Messaging.AzureServiceBus.Configuration;
using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.Configuration;
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
    /// Adds one Azure Service Bus transport from configuration.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="role">The transport role.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="sectionPath">The Azure Service Bus configuration section.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddAzureServiceBus(
        this MessagingBuilder builder,
        IConfiguration configuration,
        MessageTransportRole role = MessageTransportRole.Consumers,
        string name = "primary",
        string sectionPath = "Messaging:AzureServiceBus")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return builder.AddAzureServiceBusBus(
            name,
            options =>
            {
                Bind(configuration, sectionPath, options);
                options.ConnectionString = ResolveConnectionString(
                    configuration,
                    primaryConnectionStringName: "servicebus",
                    fallbackConnectionStringName: "messaging",
                    options.ConnectionString);
            },
            role);
    }

    /// <summary>
    /// Adds one named Azure Service Bus transport.
    /// </summary>
    /// <param name="builder">The messaging builder.</param>
    /// <param name="name">The logical bus name.</param>
    /// <param name="configure">The bus configuration delegate.</param>
    /// <param name="role">The runtime role for the transport.</param>
    /// <returns>The messaging builder.</returns>
    public static MessagingBuilder AddAzureServiceBusBus(
        this MessagingBuilder builder,
        string name,
        Action<AzureServiceBusBusOptions> configure,
        MessageTransportRole role = MessageTransportRole.Consumers)
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

        if (role is MessageTransportRole.SendOnly or MessageTransportRole.Consumers)
        {
            builder.Services.AddKeyedSingleton<IMessageBusTransport>(name, static (services, key) =>
                new AzureServiceBusMessageBusTransport(
                    (string)key!,
                    services.GetRequiredKeyedService<AzureServiceBusClientProvider>(key!),
                    services.GetRequiredService<IMessageConventions>()));
        }

        if (role is MessageTransportRole.Consumers or MessageTransportRole.Administration)
        {
            builder.Services.AddSingleton<IMessageTopologyManager>(services =>
                new AzureServiceBusTopologyManager(
                    name,
                    services.GetRequiredKeyedService<AzureServiceBusClientProvider>(name),
                    services.GetRequiredService<IMessageTopologyManifest>(),
                    services.GetRequiredService<IMessageRouteResolver>(),
                    services.GetRequiredService<IMessageConventions>(),
                    services.GetRequiredService<IOptions<MessagingOptions>>()));
        }

        if (role is MessageTransportRole.Consumers)
        {
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
        }

        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                $"layerzero.messaging.azure-service-bus.{name}",
                services => new AzureServiceBusHealthCheck(name, services.GetRequiredKeyedService<AzureServiceBusClientProvider>(name)),
                HealthStatus.Unhealthy,
                ["messaging", "azure-service-bus", name]));

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
