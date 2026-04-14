namespace LayerZero.Data;

/// <summary>
/// Identifies a relational table.
/// </summary>
/// <param name="Schema">The optional schema name.</param>
/// <param name="Name">The table name.</param>
public sealed record QualifiedTableName(string? Schema, string Name);

/// <summary>
/// Describes a relational table column.
/// </summary>
/// <param name="Name">The column name.</param>
/// <param name="Type">The provider-neutral column type.</param>
/// <param name="IsNullable">Whether the column allows null values.</param>
/// <param name="IsIdentity">Whether the column is identity-backed.</param>
/// <param name="DefaultValue">The optional default value.</param>
public sealed record ColumnDefinition(
    string Name,
    ColumnType Type,
    bool IsNullable,
    bool IsIdentity,
    object? DefaultValue);

/// <summary>
/// Represents one provider-neutral column type.
/// </summary>
public sealed class ColumnType : IEquatable<ColumnType>
{
    private ColumnType(
        RelationalTypeKind kind,
        int? length = null,
        bool unicode = true,
        int? precision = null,
        int? scale = null)
    {
        Kind = kind;
        Length = length;
        Unicode = unicode;
        Precision = precision;
        Scale = scale;
    }

    /// <summary>
    /// Gets the logical relational type kind.
    /// </summary>
    public RelationalTypeKind Kind { get; }

    /// <summary>
    /// Gets the optional length.
    /// </summary>
    public int? Length { get; }

    /// <summary>
    /// Gets whether string data is Unicode.
    /// </summary>
    public bool Unicode { get; }

    /// <summary>
    /// Gets the optional decimal precision.
    /// </summary>
    public int? Precision { get; }

    /// <summary>
    /// Gets the optional decimal scale.
    /// </summary>
    public int? Scale { get; }

    /// <summary>
    /// Gets the provider-neutral 32-bit integer type.
    /// </summary>
    public static ColumnType Int32 { get; } = new(RelationalTypeKind.Int32);

    /// <summary>
    /// Gets the provider-neutral 64-bit integer type.
    /// </summary>
    public static ColumnType Int64 { get; } = new(RelationalTypeKind.Int64);

    /// <summary>
    /// Gets the provider-neutral Boolean type.
    /// </summary>
    public static ColumnType Boolean { get; } = new(RelationalTypeKind.Boolean);

    /// <summary>
    /// Gets the provider-neutral GUID type.
    /// </summary>
    public static ColumnType Guid { get; } = new(RelationalTypeKind.Guid);

    /// <summary>
    /// Gets the provider-neutral date-time type.
    /// </summary>
    public static ColumnType DateTime { get; } = new(RelationalTypeKind.DateTime);

    /// <summary>
    /// Gets the provider-neutral date-time-offset type.
    /// </summary>
    public static ColumnType DateTimeOffset { get; } = new(RelationalTypeKind.DateTimeOffset);

    /// <summary>
    /// Creates a provider-neutral string type.
    /// </summary>
    /// <param name="length">The optional length.</param>
    /// <param name="unicode">Whether the string is Unicode.</param>
    /// <returns>The created type.</returns>
    public static ColumnType String(int? length = null, bool unicode = true) =>
        new(RelationalTypeKind.String, length: length, unicode: unicode);

    /// <summary>
    /// Creates a provider-neutral decimal type.
    /// </summary>
    /// <param name="precision">The decimal precision.</param>
    /// <param name="scale">The decimal scale.</param>
    /// <returns>The created type.</returns>
    public static ColumnType Decimal(int precision = 18, int scale = 2) =>
        new(RelationalTypeKind.Decimal, precision: precision, scale: scale);

    /// <summary>
    /// Creates a provider-neutral binary type.
    /// </summary>
    /// <param name="length">The optional length.</param>
    /// <returns>The created type.</returns>
    public static ColumnType Binary(int? length = null) =>
        new(RelationalTypeKind.Binary, length: length);

    /// <inheritdoc />
    public bool Equals(ColumnType? other)
    {
        return other is not null
            && Kind == other.Kind
            && Length == other.Length
            && Unicode == other.Unicode
            && Precision == other.Precision
            && Scale == other.Scale;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ColumnType);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Length, Unicode, Precision, Scale);
}

/// <summary>
/// Identifies a provider-neutral relational type.
/// </summary>
public enum RelationalTypeKind
{
    /// <summary>
    /// A 32-bit integer.
    /// </summary>
    Int32 = 0,

    /// <summary>
    /// A 64-bit integer.
    /// </summary>
    Int64 = 1,

    /// <summary>
    /// A decimal number.
    /// </summary>
    Decimal = 2,

    /// <summary>
    /// A string.
    /// </summary>
    String = 3,

    /// <summary>
    /// A Boolean.
    /// </summary>
    Boolean = 4,

    /// <summary>
    /// A GUID.
    /// </summary>
    Guid = 5,

    /// <summary>
    /// A date-time.
    /// </summary>
    DateTime = 6,

    /// <summary>
    /// A date-time with offset.
    /// </summary>
    DateTimeOffset = 7,

    /// <summary>
    /// A binary payload.
    /// </summary>
    Binary = 8,
}
