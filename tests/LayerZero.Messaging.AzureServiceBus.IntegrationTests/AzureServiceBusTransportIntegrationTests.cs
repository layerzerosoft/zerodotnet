using LayerZero.Messaging.IntegrationTesting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.ServiceBus;
using Xunit.Sdk;

namespace LayerZero.Messaging.AzureServiceBus.IntegrationTests;

public sealed class AzureServiceBusTransportIntegrationTests(ServiceBusFixture fixture) : MessageTransportIntegrationTestBase, IClassFixture<ServiceBusFixture>
{
    protected override string BrokerName => "servicebus";

    protected override IHost CreateHost(string applicationName, IntegrationState? state = null)
    {
        return IntegrationTestHost.Build(
            applicationName,
            builder => builder.AddAzureServiceBusBus("primary", options =>
            {
                options.ConnectionString = fixture.Container.GetConnectionString();
                options.AdministrationConnectionString = fixture.Container.GetHttpConnectionString();
                options.PrefetchCount = 8;
                options.MaxConcurrentCalls = 2;
                options.MaxAutoLockRenewalDuration = TimeSpan.FromSeconds(30);
                options.MaxDeliveryCount = 2;
            }),
            configureServices: services =>
            {
                if (state is not null)
                {
                    services.AddSingleton(state);
                }
            });
    }
}

public sealed class ServiceBusFixture : TestcontainerFixtureBase<ServiceBusContainer>
{
    public ServiceBusFixture()
        : base("LayerZero.Messaging.AzureServiceBus.IntegrationTests", "servicebus")
    {
    }

    protected override ValueTask<ServiceBusContainer> CreateContainerAsync(TestcontainerFixtureMetadata metadata)
    {
        return ValueTask.FromResult(
            ApplyContainerDefaults(new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest"))
                .WithAcceptLicenseAgreement(true)
                .Build());
    }
}

public sealed class CloudAzureServiceBusTransportIntegrationTests(CloudServiceBusFixture fixture) : MessageTransportIntegrationTestBase, IClassFixture<CloudServiceBusFixture>
{
    protected override string BrokerName => "servicebus-cloud";

    protected override IHost CreateHost(string applicationName, IntegrationState? state = null)
    {
        return IntegrationTestHost.Build(
            applicationName,
            builder => builder.AddAzureServiceBusBus("primary", options =>
            {
                options.ConnectionString = fixture.ConnectionString;
                options.AdministrationConnectionString = fixture.AdministrationConnectionString;
                options.PrefetchCount = 8;
                options.MaxConcurrentCalls = 2;
                options.MaxAutoLockRenewalDuration = TimeSpan.FromSeconds(30);
                options.MaxDeliveryCount = 2;
            }),
            configureServices: services =>
            {
                if (state is not null)
                {
                    services.AddSingleton(state);
                }
            });
    }
}

public sealed class CloudServiceBusFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public string AdministrationConnectionString { get; private set; } = string.Empty;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public ValueTask InitializeAsync()
    {
        ConnectionString = Environment.GetEnvironmentVariable("LAYERZERO_AZURE_SERVICE_BUS_CLOUD_CONNECTION_STRING") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw SkipException.ForSkip("Set LAYERZERO_AZURE_SERVICE_BUS_CLOUD_CONNECTION_STRING to run Azure Service Bus cloud parity tests.");
        }

        AdministrationConnectionString = Environment.GetEnvironmentVariable("LAYERZERO_AZURE_SERVICE_BUS_CLOUD_ADMIN_CONNECTION_STRING")
            ?? ConnectionString;
        return ValueTask.CompletedTask;
    }
}
