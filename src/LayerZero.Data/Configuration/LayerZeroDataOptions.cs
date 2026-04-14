namespace LayerZero.Data.Configuration;

/// <summary>
/// Configures the shared LayerZero data foundation.
/// </summary>
public sealed class LayerZeroDataOptions
{
    /// <summary>
    /// Gets or sets the logical connection string name used by provider packages.
    /// </summary>
    public string ConnectionStringName { get; set; } = "Default";

    /// <summary>
    /// Gets or sets the configured provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;
}
