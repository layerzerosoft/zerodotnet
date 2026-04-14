using System.Collections.ObjectModel;
using LayerZero.Data;

namespace LayerZero.Migrations;

/// <summary>
/// Builds one typed entity row.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
public sealed class EntityRowBuilder<TEntity>
{
    private readonly Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sets one typed column value.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="column">The mapped column.</param>
    /// <param name="value">The column value.</param>
    /// <returns>The current builder.</returns>
    public EntityRowBuilder<TEntity> Set<TProperty>(EntityColumn<TEntity, TProperty> column, TProperty value)
    {
        ArgumentNullException.ThrowIfNull(column);
        values[column.Name] = value;
        return this;
    }

    /// <summary>
    /// Sets one typed column value.
    /// </summary>
    /// <param name="column">The mapped column.</param>
    /// <param name="value">The column value.</param>
    /// <returns>The current builder.</returns>
    public EntityRowBuilder<TEntity> Set(EntityColumn<TEntity> column, object? value)
    {
        ArgumentNullException.ThrowIfNull(column);
        values[column.Name] = value;
        return this;
    }

    internal ColumnValueSet Build()
    {
        return new ColumnValueSet(new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase)));
    }
}

/// <summary>
/// Builds a deterministic set of typed entity rows.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
public sealed class EntityRowSetBuilder<TEntity>
{
    private readonly List<ColumnValueSet> rows = [];

    /// <summary>
    /// Adds one typed row to the set.
    /// </summary>
    /// <param name="configure">The row configuration delegate.</param>
    /// <returns>The current builder.</returns>
    public EntityRowSetBuilder<TEntity> Row(Action<EntityRowBuilder<TEntity>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new EntityRowBuilder<TEntity>();
        configure(builder);
        rows.Add(builder.Build());
        return this;
    }

    internal IReadOnlyList<ColumnValueSet> Build() => rows.AsReadOnly();
}
