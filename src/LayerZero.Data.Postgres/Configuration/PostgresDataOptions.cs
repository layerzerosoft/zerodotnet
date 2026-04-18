namespace LayerZero.Data.Postgres.Configuration;

/// <summary>
/// Configures LayerZero PostgreSQL data services.
/// </summary>
public sealed class PostgresDataOptions
{
    /// <summary>
    /// Gets or sets the explicit PostgreSQL connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the logical connection string name.
    /// </summary>
    public string ConnectionStringName { get; set; } = "Default";

    /// <summary>
    /// Gets or sets the default PostgreSQL schema.
    /// </summary>
    public string DefaultSchema { get; set; } = "public";
}
