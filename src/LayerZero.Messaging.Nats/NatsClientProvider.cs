using LayerZero.Messaging.Nats.Configuration;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Net;

namespace LayerZero.Messaging.Nats;

internal sealed class NatsClientProvider(string name, IOptionsMonitor<NatsBusOptions> optionsMonitor) : IAsyncDisposable
{
    private readonly string busName = name;
    private readonly IOptionsMonitor<NatsBusOptions> optionsMonitor = optionsMonitor;
    private readonly SemaphoreSlim gate = new(1, 1);
    private NatsClient? client;
    private INatsJSContext? jetStream;

    public NatsBusOptions Options => optionsMonitor.Get(busName);

    public async ValueTask<NatsClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (client is not null)
        {
            return client;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (client is not null)
            {
                return client;
            }

            client = new NatsClient(new NatsOpts
            {
                Url = Options.Url,
                Name = $"layerzero-{busName}",
            });

            await client.ConnectAsync().ConfigureAwait(false);
            jetStream = client.CreateJetStreamContext();
            return client;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<INatsJSContext> GetJetStreamAsync(CancellationToken cancellationToken)
    {
        if (jetStream is not null)
        {
            return jetStream;
        }

        await GetClientAsync(cancellationToken).ConfigureAwait(false);
        return jetStream!;
    }

    public async ValueTask DisposeAsync()
    {
        if (client is not null)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }

        gate.Dispose();
    }
}
