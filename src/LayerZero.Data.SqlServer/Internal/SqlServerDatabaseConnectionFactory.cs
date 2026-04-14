using System.Data.Common;
using LayerZero.Data.SqlServer.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.SqlServer.Internal;

internal sealed class SqlServerDatabaseConnectionFactory(IOptions<SqlServerDataOptions> optionsAccessor) : IDatabaseConnectionFactory
{
    private readonly SqlServerDataOptions options = optionsAccessor.Value;

    public string ProviderName => SqlServerDataProvider.ProviderName;

    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = options.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The SQL Server connection string is not configured.");
        }

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
