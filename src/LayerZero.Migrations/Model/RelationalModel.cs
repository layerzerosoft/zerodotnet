using System.Collections.ObjectModel;
using LayerZero.Data;

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
