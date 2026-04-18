using System.Data.Common;
using LayerZero.Data.Postgres.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LayerZero.Data.Postgres.Internal;

internal sealed class PostgresDatabaseConnectionFactory(
    NpgsqlDataSource dataSource,
    IOptions<PostgresDataOptions> optionsAccessor) : IDatabaseConnectionFactory
{
    private readonly NpgsqlDataSource dataSource = dataSource;
    private readonly PostgresDataOptions options = optionsAccessor.Value;

    public string ProviderName => PostgresDataProvider.ProviderName;

    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("The PostgreSQL connection string is not configured.");
        }

        return await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    }
}
