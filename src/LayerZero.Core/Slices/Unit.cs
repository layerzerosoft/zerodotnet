namespace LayerZero.Core;

/// <summary>
/// Represents an explicit request value for endpoints that do not need input.
/// </summary>
public readonly record struct Unit
{
    /// <summary>
    /// Gets the single logical unit value.
    /// </summary>
    public static Unit Value { get; } = new();
}
