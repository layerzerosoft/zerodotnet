namespace LayerZero.Migrations.Postgres.Configuration;

/// <summary>
/// Configures the LayerZero PostgreSQL migrations adapter.
/// </summary>
public sealed class PostgresMigrationsOptions
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
