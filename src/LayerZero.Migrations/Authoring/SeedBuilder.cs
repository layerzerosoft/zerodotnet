namespace LayerZero.Migrations;

/// <summary>
/// Builds one seed operation list.
/// </summary>
public sealed class SeedBuilder
{
    private readonly MigrationBuilder inner = new();

    /// <summary>
    /// Ensures a schema exists.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder EnsureSchema(string schema)
    {
        inner.EnsureSchema(schema);
        return this;
    }

    /// <summary>
    /// Inserts rows into a table in the default schema.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="configure">The row set configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder InsertData(string tableName, Action<DataRowSetBuilder> configure)
    {
        inner.InsertData(tableName, configure);
        return this;
    }

    /// <summary>
    /// Inserts rows into a table in an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="configure">The row set configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder InsertData(string? schema, string tableName, Action<DataRowSetBuilder> configure)
    {
        inner.InsertData(schema, tableName, configure);
        return this;
    }

    /// <summary>
    /// Updates one row.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="key">The key predicate configuration.</param>
    /// <param name="values">The updated values configuration.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder UpdateData(string tableName, Action<DataRowBuilder> key, Action<DataRowBuilder> values)
    {
        inner.UpdateData(tableName, key, values);
        return this;
    }

    /// <summary>
    /// Updates one row.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="key">The key predicate configuration.</param>
    /// <param name="values">The updated values configuration.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder UpdateData(string? schema, string tableName, Action<DataRowBuilder> key, Action<DataRowBuilder> values)
    {
        inner.UpdateData(schema, tableName, key, values);
        return this;
    }

    /// <summary>
    /// Deletes one row.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="key">The key predicate configuration.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder DeleteData(string tableName, Action<DataRowBuilder> key)
    {
        inner.DeleteData(tableName, key);
        return this;
    }

    /// <summary>
    /// Deletes one row.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="key">The key predicate configuration.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder DeleteData(string? schema, string tableName, Action<DataRowBuilder> key)
    {
        inner.DeleteData(schema, tableName, key);
        return this;
    }

    /// <summary>
    /// Upserts one row.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="keyColumns">The key columns.</param>
    /// <param name="values">The row values configuration.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder UpsertData(string tableName, IEnumerable<string> keyColumns, Action<DataRowBuilder> values)
    {
        inner.UpsertData(tableName, keyColumns, values);
        return this;
    }

    /// <summary>
    /// Upserts one row.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="keyColumns">The key columns.</param>
    /// <param name="values">The row values configuration.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder UpsertData(string? schema, string tableName, IEnumerable<string> keyColumns, Action<DataRowBuilder> values)
    {
        inner.UpsertData(schema, tableName, keyColumns, values);
        return this;
    }

    /// <summary>
    /// Synchronizes a table.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="keyColumns">The key columns.</param>
    /// <param name="configure">The row set configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder SyncData(string tableName, IEnumerable<string> keyColumns, Action<DataRowSetBuilder> configure)
    {
        inner.SyncData(tableName, keyColumns, configure);
        return this;
    }

    /// <summary>
    /// Synchronizes a table.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <param name="keyColumns">The key columns.</param>
    /// <param name="configure">The row set configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder SyncData(string? schema, string tableName, IEnumerable<string> keyColumns, Action<DataRowSetBuilder> configure)
    {
        inner.SyncData(schema, tableName, keyColumns, configure);
        return this;
    }

    /// <summary>
    /// Emits raw SQL.
    /// </summary>
    /// <param name="sql">The raw SQL text.</param>
    /// <returns>The current builder.</returns>
    public SeedBuilder Sql(string sql)
    {
        inner.Sql(sql);
        return this;
    }

    internal IReadOnlyList<RelationalOperation> Build() => inner.Build();
}
