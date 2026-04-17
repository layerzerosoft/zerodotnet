namespace LayerZero.Data;

/// <summary>
/// Represents one joined row.
/// </summary>
/// <typeparam name="TLeft">The left row type.</typeparam>
/// <typeparam name="TRight">The right row type.</typeparam>
public sealed record DataJoin<TLeft, TRight>(TLeft Left, TRight Right);
