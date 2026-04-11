using LayerZero.Messaging.Configuration;
using LayerZero.Messaging;
using LayerZero.Messaging.AzureServiceBus;
using LayerZero.Messaging.Kafka;
using LayerZero.Messaging.Nats;
using LayerZero.Messaging.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LayerZero.Fulfillment.Shared;

public static class FulfillmentMessagingRegistration
{
    public static string ResolveApplicationName(IConfiguration configuration, string role, string defaultName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultName);

        var explicitName = configuration[$"Messaging:ApplicationName:{role}"];
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName;
        }

        var rootName = configuration["Messaging:ApplicationName"];
        return string.IsNullOrWhiteSpace(rootName)
            ? defaultName
            : $"{rootName}-{role}";
    }

    public static MessagingBuilder AddFulfillmentMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        var broker = configuration["Messaging:Broker"] ?? "RabbitMq";
        var builder = services.AddMessaging(options => options.ApplicationName = applicationName);

        return broker switch
        {
            "RabbitMq" => builder.AddRabbitMqBus("primary", options =>
            {
                Bind(configuration, "Messaging:RabbitMq", options);
                options.ConnectionString = ResolveConnectionString(
                    options.ConnectionString,
                    configuration.GetConnectionString("rabbitmq"),
                    configuration.GetConnectionString("messaging"));
            }),
            "AzureServiceBus" => builder.AddAzureServiceBusBus("primary", options =>
            {
                Bind(configuration, "Messaging:AzureServiceBus", options);
                options.ConnectionString = ResolveConnectionString(
                    options.ConnectionString,
                    configuration.GetConnectionString("servicebus"),
                    configuration.GetConnectionString("messaging"));
            }),
            "Kafka" => builder.AddKafkaBus("primary", options =>
            {
                Bind(configuration, "Messaging:Kafka", options);
                options.BootstrapServers = ResolveConnectionString(
                    options.BootstrapServers,
                    configuration.GetConnectionString("kafka"),
                    configuration.GetConnectionString("messaging"));
            }),
            "Nats" => builder.AddNatsBus("primary", options =>
            {
                Bind(configuration, "Messaging:Nats", options);
                options.Url = ResolveConnectionString(
                    options.Url,
                    configuration.GetConnectionString("nats"),
                    configuration.GetConnectionString("messaging"));
            }),
            _ => throw new InvalidOperationException($"Unsupported broker '{broker}'."),
        };
    }

    public static IServiceCollection AddFulfillmentStore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Fulfillment")
            ?? "Data Source=fulfillment.db";

        services.TryAddSingleton<IMessageRegistry, FulfillmentMessageRegistry>();
        services.AddSingleton(servicesProvider => new FulfillmentStore(
            connectionString,
            servicesProvider.GetService<IMessageContextAccessor>(),
            servicesProvider.GetService<IMessageRegistry>(),
            servicesProvider.GetService<IMessageConventions>()));
        services.AddSingleton<IMessageIdempotencyStore, SqliteMessageIdempotencyStore>();
        services.AddSingleton<IMessageSettlementObserver, SqliteSettlementObserver>();
        services.AddScoped<DeadLetterReplayService>();
        return services;
    }

    private static void Bind<TOptions>(IConfiguration configuration, string sectionPath, TOptions options)
        where TOptions : class
    {
        configuration.GetSection(sectionPath).Bind(options);
    }

    private static string ResolveConnectionString(string? configuredValue, params string?[] fallbacks)
    {
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return configuredValue;
        }

        foreach (var fallback in fallbacks)
        {
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }
        }

        return configuredValue ?? string.Empty;
    }
}
