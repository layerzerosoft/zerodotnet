namespace LayerZero.Migrations.Configuration;

/// <summary>
/// Configures LayerZero relational migrations.
/// </summary>
public sealed class MigrationsOptions
{
    /// <summary>
    /// Gets or sets the schema that stores the LayerZero migration history table.
    /// </summary>
    public string HistoryTableSchema { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the LayerZero migration history table name.
    /// </summary>
    public string HistoryTableName { get; set; } = "__LayerZeroMigrationsHistory";

    /// <summary>
    /// Gets or sets the logical lock name used to serialize migration runners.
    /// </summary>
    public string LockName { get; set; } = "layerzero.migrations";

    /// <summary>
    /// Gets or sets the executor name written into migration history rows.
    /// </summary>
    public string Executor { get; set; } =
        AppDomain.CurrentDomain.FriendlyName is { Length: > 0 } friendlyName
            ? friendlyName
            : "layerzero";
}
