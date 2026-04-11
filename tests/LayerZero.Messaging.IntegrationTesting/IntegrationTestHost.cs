using LayerZero.Messaging.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayerZero.Messaging.IntegrationTesting;

public static class IntegrationTestHost
{
    public static IHost Build(
        string applicationName,
        Action<MessagingBuilder> addTransport,
        Action<MessagingOptions>? configureMessaging = null,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentNullException.ThrowIfNull(addTransport);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IntegrationState>();
        builder.Services.AddSingleton<IMessageIdempotencyStore, InMemoryMessageIdempotencyStore>();
        builder.Services.AddSingleton<IMessageSettlementObserver, IntegrationSettlementObserver>();

        var messaging = builder.Services.AddMessaging(options =>
        {
            options.ApplicationName = applicationName;
            configureMessaging?.Invoke(options);
        });

        addTransport(messaging);
        builder.Services.AddMessages();
        configureServices?.Invoke(builder.Services);
        return builder.Build();
    }

    public static async Task ProvisionAsync(IHost host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        using var scope = host.Services.CreateScope();
        foreach (var manager in scope.ServiceProvider.GetServices<IMessageTopologyManager>())
        {
            await manager.ProvisionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task ValidateAsync(IHost host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        using var scope = host.Services.CreateScope();
        foreach (var manager in scope.ServiceProvider.GetServices<IMessageTopologyManager>())
        {
            await manager.ValidateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
