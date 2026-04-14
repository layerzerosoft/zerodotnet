namespace LayerZero.Data.SqlServer.Configuration;

/// <summary>
/// Configures LayerZero SQL Server data services.
/// </summary>
public sealed class SqlServerDataOptions
{
    /// <summary>
    /// Gets or sets the explicit SQL Server connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the logical connection string name.
    /// </summary>
    public string ConnectionStringName { get; set; } = "Default";

    /// <summary>
    /// Gets or sets the default SQL Server schema.
    /// </summary>
    public string DefaultSchema { get; set; } = "dbo";
}
