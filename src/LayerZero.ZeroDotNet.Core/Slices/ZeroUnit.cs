namespace LayerZero.ZeroDotNet;

/// <summary>
/// Represents an explicit request value for endpoints that do not need input.
/// </summary>
public readonly record struct ZeroUnit
{
    /// <summary>
    /// Gets the single logical unit value.
    /// </summary>
    public static ZeroUnit Value { get; } = new();
}
