namespace LayerZero.Data.Configuration;

/// <summary>
/// Configures the shared LayerZero data foundation.
/// </summary>
public sealed class DataOptions
{
    /// <summary>
    /// Gets or sets the shared data conventions.
    /// </summary>
    public DataConventionsOptions Conventions { get; set; } = new();

    /// <summary>
    /// Gets or sets the provider-neutral connection string override.
    /// </summary>
    public string? ConnectionString { get; set; }
}

/// <summary>
/// Configures provider-neutral data conventions.
/// </summary>
public sealed class DataConventionsOptions
{
    /// <summary>
    /// Gets or sets the table naming convention.
    /// </summary>
    public DataIdentifierNamingConvention TableNaming { get; set; } = DataIdentifierNamingConvention.Exact;

    /// <summary>
    /// Gets or sets the column naming convention.
    /// </summary>
    public DataIdentifierNamingConvention ColumnNaming { get; set; } = DataIdentifierNamingConvention.Exact;

    /// <summary>
    /// Uses exact CLR names for table and column inference.
    /// </summary>
    public void UseExactIdentifiers()
    {
        TableNaming = DataIdentifierNamingConvention.Exact;
        ColumnNaming = DataIdentifierNamingConvention.Exact;
    }

    /// <summary>
    /// Uses snake_case names for table and column inference.
    /// </summary>
    public void UseSnakeCaseIdentifiers()
    {
        TableNaming = DataIdentifierNamingConvention.SnakeCase;
        ColumnNaming = DataIdentifierNamingConvention.SnakeCase;
    }

    internal string GetTableName(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return ApplyConvention(entityType.Name, TableNaming);
    }

    internal string GetColumnName(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return ApplyConvention(propertyName, ColumnNaming);
    }

    internal DataConventionsOptions Clone() =>
        new()
        {
            TableNaming = TableNaming,
            ColumnNaming = ColumnNaming,
        };

    private static string ApplyConvention(string name, DataIdentifierNamingConvention convention) =>
        convention switch
        {
            DataIdentifierNamingConvention.Exact => name,
            DataIdentifierNamingConvention.SnakeCase => Internal.ExpressionHelpers.ToSnakeLikeName(name),
            _ => throw new InvalidOperationException($"Unsupported data naming convention '{convention}'."),
        };
}

/// <summary>
/// Identifies one data identifier naming convention.
/// </summary>
public enum DataIdentifierNamingConvention
{
    /// <summary>
    /// Uses the exact CLR identifier.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// Uses snake_case identifiers.
    /// </summary>
    SnakeCase = 1,
}
