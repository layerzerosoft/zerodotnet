namespace LayerZero.Migrations.SqlServer.Configuration;

/// <summary>
/// Configures the LayerZero SQL Server migrations adapter.
/// </summary>
public sealed class SqlServerMigrationsOptions
{
    /// <summary>
    /// Gets or sets the database lock timeout.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the optional SQL command timeout in seconds.
    /// </summary>
    public int? CommandTimeoutSeconds { get; set; }
}
