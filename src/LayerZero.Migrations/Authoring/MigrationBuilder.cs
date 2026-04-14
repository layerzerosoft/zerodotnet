namespace LayerZero.Migrations;

/// <summary>
/// Builds one migration operation list.
/// </summary>
public sealed class MigrationBuilder
{
    private readonly List<RelationalOperation> operations = [];

    /// <summary>
    /// Ensures a schema exists.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder EnsureSchema(string schema)
    {
        operations.Add(new EnsureSchemaOperation(schema));
        return this;
    }

    /// <summary>
    /// Creates a table in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="configure">The table configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder CreateTable(string tableName, Action<TableBuilder> configure) =>
        CreateTable(schema: null, tableName, configure);

    /// <summary>
    /// Creates a table in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="configure">The table configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder CreateTable(string? schema, string tableName, Action<TableBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(configure);

        var table = new TableBuilder();
        configure(table);
        operations.Add(new CreateTableOperation(
            new QualifiedTableName(schema, tableName),
            table.BuildColumns(),
            table.BuildPrimaryKeyColumns()));
        return this;
    }

    /// <summary>
    /// Drops a table in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder DropTable(string tableName) => DropTable(schema: null, tableName);

    /// <summary>
    /// Drops a table in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder DropTable(string? schema, string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        operations.Add(new DropTableOperation(new QualifiedTableName(schema, tableName)));
        return this;
    }

    /// <summary>
    /// Adds a column in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="configure">The column configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder AddColumn(string tableName, string columnName, Action<ColumnBuilder> configure) =>
        AddColumn(schema: null, tableName, columnName, configure);

    /// <summary>
    /// Adds a column in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="configure">The column configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder AddColumn(string? schema, string tableName, string columnName, Action<ColumnBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        ArgumentNullException.ThrowIfNull(configure);

        var column = new ColumnBuilder(columnName);
        configure(column);
        operations.Add(new AddColumnOperation(new QualifiedTableName(schema, tableName), column.Build()));
        return this;
    }

    /// <summary>
    /// Creates an index in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="columns">The index columns.</param>
    /// <param name="isUnique">Whether the index is unique.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder CreateIndex(
        string tableName,
        string indexName,
        IEnumerable<string> columns,
        bool isUnique = false) =>
        CreateIndex(schema: null, tableName, indexName, columns, isUnique);

    /// <summary>
    /// Creates an index in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="columns">The index columns.</param>
    /// <param name="isUnique">Whether the index is unique.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder CreateIndex(
        string? schema,
        string tableName,
        string indexName,
        IEnumerable<string> columns,
        bool isUnique = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        ArgumentNullException.ThrowIfNull(columns);

        operations.Add(new CreateIndexOperation(
            new QualifiedTableName(schema, tableName),
            indexName,
            columns.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            isUnique));
        return this;
    }

    /// <summary>
    /// Drops an index in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder DropIndex(string tableName, string indexName) =>
        DropIndex(schema: null, tableName, indexName);

    /// <summary>
    /// Drops an index in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="indexName">The index name.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder DropIndex(string? schema, string tableName, string indexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        operations.Add(new DropIndexOperation(new QualifiedTableName(schema, tableName), indexName));
        return this;
    }

    /// <summary>
    /// Inserts rows into a table in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="configure">The row set configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder InsertData(string tableName, Action<DataRowSetBuilder> configure) =>
        InsertData(schema: null, tableName, configure);

    /// <summary>
    /// Inserts rows into a table in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="configure">The row set configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder InsertData(string? schema, string tableName, Action<DataRowSetBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(configure);

        var rows = new DataRowSetBuilder();
        configure(rows);
        operations.Add(new InsertDataOperation(new QualifiedTableName(schema, tableName), rows.Build()));
        return this;
    }

    /// <summary>
    /// Updates one row in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="key">The key predicate configuration.</param>
    /// <param name="values">The updated values configuration.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder UpdateData(
        string tableName,
        Action<DataRowBuilder> key,
        Action<DataRowBuilder> values) =>
        UpdateData(schema: null, tableName, key, values);

    /// <summary>
    /// Updates one row in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="key">The key predicate configuration.</param>
    /// <param name="values">The updated values configuration.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder UpdateData(
        string? schema,
        string tableName,
        Action<DataRowBuilder> key,
        Action<DataRowBuilder> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        operations.Add(new UpdateDataOperation(
            new QualifiedTableName(schema, tableName),
            ColumnValueSet.Create(key),
            ColumnValueSet.Create(values)));
        return this;
    }

    /// <summary>
    /// Deletes one row in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="key">The key predicate configuration.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder DeleteData(string tableName, Action<DataRowBuilder> key) =>
        DeleteData(schema: null, tableName, key);

    /// <summary>
    /// Deletes one row in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="key">The key predicate configuration.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder DeleteData(string? schema, string tableName, Action<DataRowBuilder> key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        operations.Add(new DeleteDataOperation(
            new QualifiedTableName(schema, tableName),
            ColumnValueSet.Create(key)));
        return this;
    }

    /// <summary>
    /// Upserts one row in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="keyColumns">The key columns.</param>
    /// <param name="values">The row values configuration.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder UpsertData(
        string tableName,
        IEnumerable<string> keyColumns,
        Action<DataRowBuilder> values) =>
        UpsertData(schema: null, tableName, keyColumns, values);

    /// <summary>
    /// Upserts one row in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="keyColumns">The key columns.</param>
    /// <param name="values">The row values configuration.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder UpsertData(
        string? schema,
        string tableName,
        IEnumerable<string> keyColumns,
        Action<DataRowBuilder> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(keyColumns);
        operations.Add(new UpsertDataOperation(
            new QualifiedTableName(schema, tableName),
            keyColumns.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ColumnValueSet.Create(values)));
        return this;
    }

    /// <summary>
    /// Synchronizes a table in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="keyColumns">The key columns.</param>
    /// <param name="configure">The row set configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder SyncData(
        string tableName,
        IEnumerable<string> keyColumns,
        Action<DataRowSetBuilder> configure) =>
        SyncData(schema: null, tableName, keyColumns, configure);

    /// <summary>
    /// Synchronizes a table in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="keyColumns">The key columns.</param>
    /// <param name="configure">The row set configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder SyncData(
        string? schema,
        string tableName,
        IEnumerable<string> keyColumns,
        Action<DataRowSetBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(keyColumns);
        ArgumentNullException.ThrowIfNull(configure);

        var rows = new DataRowSetBuilder();
        configure(rows);
        operations.Add(new SyncDataOperation(
            new QualifiedTableName(schema, tableName),
            keyColumns.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            rows.Build()));
        return this;
    }

    /// <summary>
    /// Adds a raw SQL operation.
    /// </summary>
    /// <param name="sql">The raw SQL text.</param>
    /// <returns>The current builder.</returns>
    public MigrationBuilder Sql(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        operations.Add(new SqlOperation(sql));
        return this;
    }

    internal IReadOnlyList<RelationalOperation> Build() => operations.AsReadOnly();
}
