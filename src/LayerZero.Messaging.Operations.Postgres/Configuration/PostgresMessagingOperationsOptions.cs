namespace LayerZero.Messaging.Operations.Postgres.Configuration;

/// <summary>
/// Configures PostgreSQL-backed LayerZero messaging operations.
/// </summary>
public sealed class PostgresMessagingOperationsOptions
{
    /// <summary>
    /// Gets or sets the logical connection string name used by the surrounding application data configuration.
    /// </summary>
    public string ConnectionStringName { get; set; } = string.Empty;
}
