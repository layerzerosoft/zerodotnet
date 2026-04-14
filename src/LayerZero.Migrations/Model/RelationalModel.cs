using System.Collections.ObjectModel;

namespace LayerZero.Migrations;

/// <summary>
/// Defines a provider-neutral relational operation.
/// </summary>
public abstract record RelationalOperation;

/// <summary>
/// Ensures a schema exists.
/// </summary>
/// <param name="Schema">The schema name.</param>
public sealed record EnsureSchemaOperation(string Schema) : RelationalOperation;

/// <summary>
/// Creates a table.
/// </summary>
/// <param name="Table">The target table.</param>
/// <param name="Columns">The declared columns.</param>
/// <param name="PrimaryKeyColumns">The primary key columns.</param>
public sealed record CreateTableOperation(
    QualifiedTableName Table,
    IReadOnlyList<ColumnDefinition> Columns,
    IReadOnlyList<string> PrimaryKeyColumns) : RelationalOperation;

/// <summary>
/// Drops a table.
/// </summary>
/// <param name="Table">The target table.</param>
public sealed record DropTableOperation(QualifiedTableName Table) : RelationalOperation;

/// <summary>
/// Adds a column to a table.
/// </summary>
/// <param name="Table">The target table.</param>
/// <param name="Column">The declared column.</param>
public sealed record AddColumnOperation(QualifiedTableName Table, ColumnDefinition Column) : RelationalOperation;

/// <summary>
/// Creates an index.
/// </summary>
/// <param name="Table">The target table.</param>
/// <param name="Name">The index name.</param>
/// <param name="Columns">The indexed columns.</param>
/// <param name="IsUnique">Whether the index is unique.</param>
public sealed record CreateIndexOperation(
    QualifiedTableName Table,
    string Name,
    IReadOnlyList<string> Columns,
    bool IsUnique) : RelationalOperation;

/// <summary>
/// Drops an index.
/// </summary>
/// <param name="Table">The target table.</param>
/// <param name="Name">The index name.</param>
public sealed record DropIndexOperation(QualifiedTableName Table, string Name) : RelationalOperation;

/// <summary>
/// Inserts one or more rows.
/// </summary>
/// <param name="Table">The target table.</param>
/// <param name="Rows">The rows to insert.</param>
public sealed record InsertDataOperation(QualifiedTableName Table, IReadOnlyList<ColumnValueSet> Rows) : RelationalOperation;

/// <summary>
/// Updates one row matched by key values.
/// </summary>
/// <param name="Table">The target table.</param>
/// <param name="Key">The key predicate values.</param>
/// <param name="Values">The updated values.</param>
public sealed record UpdateDataOperation(QualifiedTableName Table, ColumnValueSet Key, ColumnValueSet Values) : RelationalOperation;

/// <summary>
/// Deletes one row matched by key values.
/// </summary>
/// <param name="Table">The target table.</param>
/// <param name="Key">The key predicate values.</param>
public sealed record DeleteDataOperation(QualifiedTableName Table, ColumnValueSet Key) : RelationalOperation;

/// <summary>
/// Upserts one row matched by key columns.
/// </summary>
/// <param name="Table">The target table.</param>
/// <param name="KeyColumns">The key column names.</param>
/// <param name="Values">The row values.</param>
public sealed record UpsertDataOperation(
    QualifiedTableName Table,
    IReadOnlyList<string> KeyColumns,
    ColumnValueSet Values) : RelationalOperation;

/// <summary>
/// Synchronizes a table to the provided rows.
/// </summary>
/// <param name="Table">The target table.</param>
/// <param name="KeyColumns">The key column names.</param>
/// <param name="Rows">The desired rows.</param>
public sealed record SyncDataOperation(
    QualifiedTableName Table,
    IReadOnlyList<string> KeyColumns,
    IReadOnlyList<ColumnValueSet> Rows) : RelationalOperation;

/// <summary>
/// Emits raw SQL for advanced cases.
/// </summary>
/// <param name="Sql">The raw SQL text.</param>
public sealed record SqlOperation(string Sql) : RelationalOperation;

/// <summary>
/// Identifies a relational table.
/// </summary>
/// <param name="Schema">The optional schema name.</param>
/// <param name="Name">The table name.</param>
public sealed record QualifiedTableName(string? Schema, string Name);

/// <summary>
/// Describes a table column.
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

/// <summary>
/// Represents one row of column values.
/// </summary>
public sealed class ColumnValueSet
{
    internal ColumnValueSet(IReadOnlyDictionary<string, object?> values)
    {
        Values = values;
    }

    /// <summary>
    /// Gets the row values keyed by column name.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Values { get; }

    internal static ColumnValueSet Create(Action<DataRowBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new DataRowBuilder();
        configure(builder);
        return builder.Build();
    }
}

/// <summary>
/// Builds one row of column values.
/// </summary>
public sealed class DataRowBuilder
{
    private readonly Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sets one column value.
    /// </summary>
    /// <param name="columnName">The column name.</param>
    /// <param name="value">The column value.</param>
    /// <returns>The current builder.</returns>
    public DataRowBuilder Set(string columnName, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        values[columnName] = value;
        return this;
    }

    internal ColumnValueSet Build()
    {
        return new ColumnValueSet(new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase)));
    }
}

/// <summary>
/// Builds a deterministic set of rows.
/// </summary>
public sealed class DataRowSetBuilder
{
    private readonly List<ColumnValueSet> rows = [];

    /// <summary>
    /// Adds one row to the set.
    /// </summary>
    /// <param name="configure">The row configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public DataRowSetBuilder Row(Action<DataRowBuilder> configure)
    {
        rows.Add(ColumnValueSet.Create(configure));
        return this;
    }

    internal IReadOnlyList<ColumnValueSet> Build() => rows.AsReadOnly();
}
