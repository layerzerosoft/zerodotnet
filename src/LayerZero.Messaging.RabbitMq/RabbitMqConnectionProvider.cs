using LayerZero.Messaging.RabbitMq.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LayerZero.Messaging.RabbitMq;

internal sealed class RabbitMqConnectionProvider(string name, IOptionsMonitor<RabbitMqBusOptions> optionsMonitor) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private static readonly CreateChannelOptions PublisherConfirmationChannelOptions = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true,
        outstandingPublisherConfirmationsRateLimiter: null,
        consumerDispatchConcurrency: null);
    private readonly string busName = name;
    private readonly IOptionsMonitor<RabbitMqBusOptions> optionsMonitor = optionsMonitor;

    public string Name => busName;

    public RabbitMqBusOptions Options => optionsMonitor.Get(busName);

    public async ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (connection is { IsOpen: true })
        {
            return connection;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (connection is { IsOpen: true })
            {
                return connection;
            }

            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(Options.ConnectionString, UriKind.Absolute),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = false,
            };

            connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IChannel> CreateChannelAsync(CancellationToken cancellationToken, bool publisherConfirmations = false)
    {
        var currentConnection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return publisherConfirmations
            ? await currentConnection.CreateChannelAsync(PublisherConfirmationChannelOptions, cancellationToken).ConfigureAwait(false)
            : await currentConnection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        gate.Dispose();
    }
}
