namespace LayerZero.Migrations.SqlServer.Configuration;

/// <summary>
/// Configures the LayerZero SQL Server migrations adapter.
/// </summary>
public sealed class SqlServerMigrationsOptions
{
    /// <summary>
    /// Gets or sets the SQL Server connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default schema used when an operation omits a schema name.
    /// </summary>
    public string DefaultSchema { get; set; } = "dbo";

    /// <summary>
    /// Gets or sets the database lock timeout.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the optional SQL command timeout in seconds.
    /// </summary>
    public int? CommandTimeoutSeconds { get; set; }
}
