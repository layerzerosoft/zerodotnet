using System.Data.Common;

namespace LayerZero.Data;

/// <summary>
/// Opens database connections for the active LayerZero data provider.
/// </summary>
public interface IDatabaseConnectionFactory
{
    /// <summary>
    /// Gets the logical provider name.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Opens a database connection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The opened connection.</returns>
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
