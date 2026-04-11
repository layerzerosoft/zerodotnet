using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using LayerZero.Messaging.AzureServiceBus.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.AzureServiceBus;

internal sealed class AzureServiceBusClientProvider(string name, IOptionsMonitor<AzureServiceBusBusOptions> optionsMonitor) : IAsyncDisposable
{
    private readonly string busName = name;
    private readonly IOptionsMonitor<AzureServiceBusBusOptions> optionsMonitor = optionsMonitor;
    private ServiceBusClient? client;
    private ServiceBusAdministrationClient? administrationClient;

    public AzureServiceBusBusOptions Options => optionsMonitor.Get(busName);

    public ServiceBusClient GetClient()
    {
        client ??= new ServiceBusClient(Options.ConnectionString, new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpTcp,
        });

        return client;
    }

    public ServiceBusAdministrationClient GetAdministrationClient()
    {
        administrationClient ??= new ServiceBusAdministrationClient(
            string.IsNullOrWhiteSpace(Options.AdministrationConnectionString)
                ? Options.ConnectionString
                : Options.AdministrationConnectionString);
        return administrationClient;
    }

    public async ValueTask DisposeAsync()
    {
        if (client is not null)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
