namespace LayerZero.Migrations;

/// <summary>
/// Configures one table column.
/// </summary>
public sealed class ColumnBuilder
{
    private ColumnType type = ColumnType.String();
    private bool isNullable = true;
    private bool isIdentity;
    private object? defaultValue;

    internal ColumnBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Gets the column name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Uses the provider-neutral 32-bit integer type.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ColumnBuilder AsInt32()
    {
        type = ColumnType.Int32;
        return this;
    }

    /// <summary>
    /// Uses the provider-neutral 64-bit integer type.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ColumnBuilder AsInt64()
    {
        type = ColumnType.Int64;
        return this;
    }

    /// <summary>
    /// Uses the provider-neutral decimal type.
    /// </summary>
    /// <param name="precision">The precision.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The current builder.</returns>
    public ColumnBuilder AsDecimal(int precision = 18, int scale = 2)
    {
        type = ColumnType.Decimal(precision, scale);
        return this;
    }

    /// <summary>
    /// Uses the provider-neutral string type.
    /// </summary>
    /// <param name="length">The optional length.</param>
    /// <param name="unicode">Whether the string is Unicode.</param>
    /// <returns>The current builder.</returns>
    public ColumnBuilder AsString(int? length = null, bool unicode = true)
    {
        type = ColumnType.String(length, unicode);
        return this;
    }

    /// <summary>
    /// Uses the provider-neutral Boolean type.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ColumnBuilder AsBoolean()
    {
        type = ColumnType.Boolean;
        return this;
    }

    /// <summary>
    /// Uses the provider-neutral GUID type.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ColumnBuilder AsGuid()
    {
        type = ColumnType.Guid;
        return this;
    }

    /// <summary>
    /// Uses the provider-neutral date-time type.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ColumnBuilder AsDateTime()
    {
        type = ColumnType.DateTime;
        return this;
    }

    /// <summary>
    /// Uses the provider-neutral date-time-offset type.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ColumnBuilder AsDateTimeOffset()
    {
        type = ColumnType.DateTimeOffset;
        return this;
    }

    /// <summary>
    /// Uses the provider-neutral binary type.
    /// </summary>
    /// <param name="length">The optional length.</param>
    /// <returns>The current builder.</returns>
    public ColumnBuilder AsBinary(int? length = null)
    {
        type = ColumnType.Binary(length);
        return this;
    }

    /// <summary>
    /// Marks the column as non-nullable.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ColumnBuilder NotNull()
    {
        isNullable = false;
        return this;
    }

    /// <summary>
    /// Marks the column as nullable.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ColumnBuilder Nullable()
    {
        isNullable = true;
        return this;
    }

    /// <summary>
    /// Marks the column as identity-backed.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ColumnBuilder Identity()
    {
        isIdentity = true;
        return this;
    }

    /// <summary>
    /// Assigns a default value.
    /// </summary>
    /// <param name="value">The default value.</param>
    /// <returns>The current builder.</returns>
    public ColumnBuilder Default(object? value)
    {
        defaultValue = value;
        return this;
    }

    internal ColumnDefinition Build()
    {
        return new ColumnDefinition(Name, type, isNullable, isIdentity, defaultValue);
    }
}
