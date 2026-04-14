using LayerZero.Data;

namespace LayerZero.Migrations;

/// <summary>
/// Configures a new table.
/// </summary>
public sealed class TableBuilder
{
    private readonly List<ColumnBuilder> columns = [];
    private readonly List<string> primaryKeyColumns = [];

    /// <summary>
    /// Adds one configured column.
    /// </summary>
    /// <param name="columnName">The column name.</param>
    /// <returns>The column builder.</returns>
    public ColumnBuilder Column(string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        var builder = new ColumnBuilder(columnName);
        columns.Add(builder);
        return builder;
    }

    /// <summary>
    /// Declares the primary key columns.
    /// </summary>
    /// <param name="columnNames">The primary key columns.</param>
    /// <returns>The current builder.</returns>
    public TableBuilder PrimaryKey(params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);
        primaryKeyColumns.Clear();
        primaryKeyColumns.AddRange(columnNames.Where(static value => !string.IsNullOrWhiteSpace(value)));
        return this;
    }

    internal IReadOnlyList<ColumnDefinition> BuildColumns()
    {
        return columns
            .Select(static column => column.Build())
            .ToArray();
    }

    internal IReadOnlyList<string> BuildPrimaryKeyColumns()
    {
        return primaryKeyColumns
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
