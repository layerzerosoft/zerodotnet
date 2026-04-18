using System.Linq.Expressions;
using System.Reflection;
using LayerZero.Data.Configuration;
using LayerZero.Data.Internal;

namespace LayerZero.Data;

/// <summary>
/// Provides non-generic access to one entity map.
/// </summary>
public interface IEntityMap
{
    /// <summary>
    /// Gets the mapped entity CLR type.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Gets the mapped entity table.
    /// </summary>
    IEntityTable Table { get; }
}

/// <summary>
/// Provides non-generic access to one entity table.
/// </summary>
public interface IEntityTable
{
    /// <summary>
    /// Gets the mapped entity CLR type.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Gets the relational table name.
    /// </summary>
    QualifiedTableName Name { get; }

    /// <summary>
    /// Gets the mapped columns.
    /// </summary>
    IReadOnlyList<IEntityColumn> Columns { get; }

    /// <summary>
    /// Gets the mapped primary key columns.
    /// </summary>
    IReadOnlyList<IEntityColumn> PrimaryKeyColumns { get; }

    /// <summary>
    /// Gets the mapped indexes.
    /// </summary>
    IReadOnlyList<IEntityIndex> Indexes { get; }
}

/// <summary>
/// Provides non-generic access to one mapped column.
/// </summary>
public interface IEntityColumn
{
    /// <summary>
    /// Gets the CLR property name.
    /// </summary>
    string PropertyName { get; }

    /// <summary>
    /// Gets the CLR property.
    /// </summary>
    PropertyInfo Property { get; }

    /// <summary>
    /// Gets the CLR property type.
    /// </summary>
    Type ClrType { get; }

    /// <summary>
    /// Gets the relational column name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the column definition.
    /// </summary>
    ColumnDefinition Definition { get; }

    /// <summary>
    /// Gets the optional value converter.
    /// </summary>
    IDataValueConverter? Converter { get; }
}

/// <summary>
/// Provides non-generic access to one mapped index.
/// </summary>
public interface IEntityIndex
{
    /// <summary>
    /// Gets the index name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the indexed columns.
    /// </summary>
    IReadOnlyList<IEntityColumn> Columns { get; }

    /// <summary>
    /// Gets whether the index is unique.
    /// </summary>
    bool IsUnique { get; }
}

/// <summary>
/// Defines one typed relational entity map.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
public abstract class EntityMap<TEntity> : IEntityMap, IConventionEntityMap
{
    private static readonly DataConventionsOptions DefaultConventions = new();
    private readonly Dictionary<ConventionCacheKey, EntityTable<TEntity>> tables = [];

    /// <summary>
    /// Gets the mapped table.
    /// </summary>
    public EntityTable<TEntity> Table => GetTable(DefaultConventions);

    /// <summary>
    /// Configures the map.
    /// </summary>
    /// <param name="builder">The entity map builder.</param>
    protected abstract void Configure(EntityMapBuilder<TEntity> builder);

    Type IEntityMap.EntityType => typeof(TEntity);

    IEntityTable IEntityMap.Table => Table;

    IEntityTable IConventionEntityMap.GetTable(DataConventionsOptions conventions) => GetTable(conventions);

    internal EntityTable<TEntity> GetTable(DataConventionsOptions conventions)
    {
        ArgumentNullException.ThrowIfNull(conventions);

        var key = new ConventionCacheKey(conventions.TableNaming, conventions.ColumnNaming);
        lock (tables)
        {
            if (!tables.TryGetValue(key, out var table))
            {
                table = BuildTable(conventions);
                tables[key] = table;
            }

            return table;
        }
    }

    private EntityTable<TEntity> BuildTable(DataConventionsOptions conventions)
    {
        var builder = new EntityMapBuilder<TEntity>(conventions.Clone());
        Configure(builder);
        return builder.Build();
    }

    private readonly record struct ConventionCacheKey(
        DataIdentifierNamingConvention TableNaming,
        DataIdentifierNamingConvention ColumnNaming);
}

internal interface IConventionEntityMap
{
    IEntityTable GetTable(DataConventionsOptions conventions);
}

/// <summary>
/// Represents one mapped relational table.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
public sealed class EntityTable<TEntity> : IEntityTable
{
    internal EntityTable(
        QualifiedTableName name,
        IReadOnlyList<EntityColumn<TEntity>> columns,
        IReadOnlyList<EntityColumn<TEntity>> primaryKeyColumns,
        IReadOnlyList<EntityIndex<TEntity>> indexes)
    {
        Name = name;
        Columns = columns;
        PrimaryKeyColumns = primaryKeyColumns;
        Indexes = indexes;
    }

    /// <summary>
    /// Gets the table name.
    /// </summary>
    public QualifiedTableName Name { get; }

    /// <summary>
    /// Gets the mapped columns.
    /// </summary>
    public IReadOnlyList<EntityColumn<TEntity>> Columns { get; }

    /// <summary>
    /// Gets the mapped primary key columns.
    /// </summary>
    public IReadOnlyList<EntityColumn<TEntity>> PrimaryKeyColumns { get; }

    /// <summary>
    /// Gets the mapped indexes.
    /// </summary>
    public IReadOnlyList<EntityIndex<TEntity>> Indexes { get; }

    Type IEntityTable.EntityType => typeof(TEntity);

    IReadOnlyList<IEntityColumn> IEntityTable.Columns => Columns;

    IReadOnlyList<IEntityColumn> IEntityTable.PrimaryKeyColumns => PrimaryKeyColumns;

    IReadOnlyList<IEntityIndex> IEntityTable.Indexes => Indexes;
}

/// <summary>
/// Represents one mapped relational column.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
public abstract class EntityColumn<TEntity> : IEntityColumn
{
    private readonly Func<TEntity, object?> getter;

    internal EntityColumn(
        PropertyInfo property,
        string propertyName,
        string name,
        ColumnDefinition definition,
        IDataValueConverter? converter,
        Func<TEntity, object?> getter)
    {
        Property = property;
        PropertyName = propertyName;
        Name = name;
        Definition = definition;
        Converter = converter;
        this.getter = getter;
    }

    /// <summary>
    /// Gets the CLR property name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the CLR property.
    /// </summary>
    public PropertyInfo Property { get; }

    /// <summary>
    /// Gets the CLR property type.
    /// </summary>
    public Type ClrType => Property.PropertyType;

    /// <summary>
    /// Gets the relational column name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the column definition.
    /// </summary>
    public ColumnDefinition Definition { get; }

    /// <summary>
    /// Gets the optional value converter.
    /// </summary>
    public IDataValueConverter? Converter { get; }

    internal object? GetClrValue(TEntity entity) => getter(entity);

    internal object? GetProviderValue(TEntity entity)
    {
        var clrValue = getter(entity);
        return ConvertToProviderValue(clrValue);
    }

    internal object? ConvertToProviderValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (Converter is not null)
        {
            return Converter.ConvertToProvider(value);
        }

        var targetType = Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
        if (targetType.IsEnum)
        {
            return Convert.ChangeType(value, Enum.GetUnderlyingType(targetType));
        }

        return value;
    }

    internal object? ConvertFromProviderValue(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        if (Converter is not null)
        {
            return Converter.ConvertFromProvider(value);
        }

        var targetType = Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
        if (targetType.IsEnum)
        {
            return Enum.ToObject(targetType, value);
        }

        return value;
    }
}

/// <summary>
/// Represents one typed mapped relational column.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
/// <typeparam name="TProperty">The mapped property type.</typeparam>
public sealed class EntityColumn<TEntity, TProperty> : EntityColumn<TEntity>
{
    internal EntityColumn(
        PropertyInfo property,
        string propertyName,
        string name,
        ColumnDefinition definition,
        IDataValueConverter? converter,
        Func<TEntity, object?> getter)
        : base(property, propertyName, name, definition, converter, getter)
    {
    }
}

/// <summary>
/// Represents one mapped relational index.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
public sealed class EntityIndex<TEntity> : IEntityIndex
{
    internal EntityIndex(string name, IReadOnlyList<EntityColumn<TEntity>> columns, bool isUnique)
    {
        Name = name;
        Columns = columns;
        IsUnique = isUnique;
    }

    /// <summary>
    /// Gets the index name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the indexed columns.
    /// </summary>
    public IReadOnlyList<EntityColumn<TEntity>> Columns { get; }

    /// <summary>
    /// Gets whether the index is unique.
    /// </summary>
    public bool IsUnique { get; }

    IReadOnlyList<IEntityColumn> IEntityIndex.Columns => Columns;
}

/// <summary>
/// Builds one typed relational entity map.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
public sealed class EntityMapBuilder<TEntity>
{
    private readonly Dictionary<string, IEntityColumnBuilder<TEntity>> columns = new(StringComparer.Ordinal);
    private readonly List<EntityIndexDefinition> indexes = [];
    private readonly DataConventionsOptions conventions;
    private string? schema;
    private string? tableName;

    /// <summary>
    /// Creates one entity map builder using the default exact-name conventions.
    /// </summary>
    public EntityMapBuilder()
        : this(new DataConventionsOptions())
    {
    }

    internal EntityMapBuilder(DataConventionsOptions conventions)
    {
        this.conventions = conventions ?? throw new ArgumentNullException(nameof(conventions));
    }

    /// <summary>
    /// Maps the entity to the default schema.
    /// </summary>
    /// <param name="name">The table name.</param>
    /// <returns>The current builder.</returns>
    public EntityMapBuilder<TEntity> ToTable(string name) => ToTable(schema: null, name);

    /// <summary>
    /// Maps the entity to an explicit schema.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="name">The table name.</param>
    /// <returns>The current builder.</returns>
    public EntityMapBuilder<TEntity> ToTable(string? schema, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        this.schema = schema;
        tableName = name;
        return this;
    }

    /// <summary>
    /// Maps one entity property.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="property">The property expression.</param>
    /// <param name="columnName">The optional relational column name.</param>
    /// <returns>The typed property builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> Property<TProperty>(
        Expression<Func<TEntity, TProperty>> property,
        string? columnName = null)
    {
        ArgumentNullException.ThrowIfNull(property);

        var propertyName = ExpressionHelpers.GetPropertyName(property);
        var propertyInfo = ExpressionHelpers.GetProperty(property);
        if (!columns.TryGetValue(propertyName, out var existing))
        {
            var builder = new EntityPropertyBuilder<TEntity, TProperty>(
                propertyInfo,
                propertyName,
                columnName ?? conventions.GetColumnName(propertyName));
            columns[propertyName] = builder;
            return builder;
        }

        if (existing is not EntityPropertyBuilder<TEntity, TProperty> typed)
        {
            throw new InvalidOperationException($"Property '{propertyName}' was already mapped with a different CLR type.");
        }

        if (!string.IsNullOrWhiteSpace(columnName))
        {
            typed.HasColumnName(columnName);
        }

        return typed;
    }

    /// <summary>
    /// Adds an index over mapped properties.
    /// </summary>
    /// <param name="name">The index name.</param>
    /// <param name="isUnique">Whether the index is unique.</param>
    /// <param name="properties">The indexed properties.</param>
    /// <returns>The current builder.</returns>
    public EntityMapBuilder<TEntity> HasIndex(
        string name,
        bool isUnique,
        params Expression<Func<TEntity, object?>>[] properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(properties);
        indexes.Add(new EntityIndexDefinition(
            name,
            properties.Select(ExpressionHelpers.GetPropertyName).ToArray(),
            isUnique));
        return this;
    }

    internal EntityTable<TEntity> Build()
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            tableName = conventions.GetTableName(typeof(TEntity));
        }

        var builtColumns = columns.Values
            .Select(static builder => builder.Build())
            .ToArray();

        var keyColumns = builtColumns
            .Where(static column => column.Definition is not null)
            .Join(
                columns.Values.Where(static builder => builder.IsKey).Select(static builder => builder.PropertyName),
                static column => column.PropertyName,
                static propertyName => propertyName,
                static (column, _) => column)
            .ToArray();

        var builtIndexes = indexes
            .Select(index =>
            {
                var indexColumns = index.PropertyNames
                    .Select(propertyName => builtColumns.First(column => column.PropertyName.Equals(propertyName, StringComparison.Ordinal)))
                    .ToArray();

                return new EntityIndex<TEntity>(index.Name, indexColumns, index.IsUnique);
            })
            .ToArray();

        return new EntityTable<TEntity>(
            new QualifiedTableName(schema, tableName),
            builtColumns,
            keyColumns,
            builtIndexes);
    }

    private sealed record EntityIndexDefinition(string Name, IReadOnlyList<string> PropertyNames, bool IsUnique);
}

/// <summary>
/// Configures one mapped entity property.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
/// <typeparam name="TProperty">The mapped property type.</typeparam>
public sealed class EntityPropertyBuilder<TEntity, TProperty> : IEntityColumnBuilder<TEntity>
{
    private readonly PropertyInfo property;
    private readonly Func<TEntity, object?> getter;
    private string columnName;
    private ColumnType type;
    private bool isNullable;
    private bool isIdentity;
    private object? defaultValue;
    private IDataValueConverter? converter;

    internal EntityPropertyBuilder(PropertyInfo property, string propertyName, string columnName)
    {
        this.property = property;
        getter = CreateGetter(property);
        PropertyName = propertyName;
        this.columnName = columnName;
        type = InferColumnType(typeof(TProperty));
        isNullable = IsNullableType(typeof(TProperty));
    }

    /// <summary>
    /// Gets the CLR property name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets whether this property is part of the primary key.
    /// </summary>
    public bool IsKey { get; private set; }

    /// <summary>
    /// Sets the relational column name.
    /// </summary>
    /// <param name="name">The column name.</param>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> HasColumnName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        columnName = name;
        return this;
    }

    /// <summary>
    /// Sets the provider-neutral column type.
    /// </summary>
    /// <param name="columnType">The column type.</param>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> HasColumnType(ColumnType columnType)
    {
        ArgumentNullException.ThrowIfNull(columnType);
        type = columnType;
        return this;
    }

    /// <summary>
    /// Uses a string column type.
    /// </summary>
    /// <param name="length">The optional length.</param>
    /// <param name="unicode">Whether the string is Unicode.</param>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> HasStringType(int? length = null, bool unicode = true)
    {
        type = ColumnType.String(length, unicode);
        return this;
    }

    /// <summary>
    /// Uses a decimal column type.
    /// </summary>
    /// <param name="precision">The precision.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> HasDecimalType(int precision = 18, int scale = 2)
    {
        type = ColumnType.Decimal(precision, scale);
        return this;
    }

    /// <summary>
    /// Marks the column as required.
    /// </summary>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> IsRequired()
    {
        isNullable = false;
        return this;
    }

    /// <summary>
    /// Marks the column as optional.
    /// </summary>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> IsOptional()
    {
        isNullable = true;
        return this;
    }

    /// <summary>
    /// Marks the column as identity-backed.
    /// </summary>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> IsIdentity()
    {
        isIdentity = true;
        return this;
    }

    /// <summary>
    /// Sets a default value.
    /// </summary>
    /// <param name="value">The default value.</param>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> HasDefaultValue(object? value)
    {
        defaultValue = value;
        return this;
    }

    /// <summary>
    /// Uses one custom value converter.
    /// </summary>
    /// <typeparam name="TProvider">The provider CLR type.</typeparam>
    /// <param name="valueConverter">The value converter.</param>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> HasConverter<TProvider>(DataValueConverter<TProperty, TProvider> valueConverter)
    {
        ArgumentNullException.ThrowIfNull(valueConverter);
        converter = valueConverter;
        return this;
    }

    /// <summary>
    /// Uses JSON string conversion for the property.
    /// </summary>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> HasJsonConversion()
    {
        converter = DataValueConverters.Json<TProperty>();
        type = ColumnType.String();
        return this;
    }

    /// <summary>
    /// Marks the property as part of the primary key.
    /// </summary>
    /// <returns>The current builder.</returns>
    public EntityPropertyBuilder<TEntity, TProperty> IsKeyPart()
    {
        IsKey = true;
        isNullable = false;
        return this;
    }

    EntityColumn<TEntity> IEntityColumnBuilder<TEntity>.Build() => Build();

    internal EntityColumn<TEntity, TProperty> Build()
    {
        return new EntityColumn<TEntity, TProperty>(
            property,
            PropertyName,
            columnName,
            new ColumnDefinition(columnName, type, isNullable, isIdentity, defaultValue),
            converter,
            getter);
    }

    private static ColumnType InferColumnType(Type type)
    {
        var clrType = Nullable.GetUnderlyingType(type) ?? type;
        if (clrType == typeof(int))
        {
            return ColumnType.Int32;
        }

        if (clrType == typeof(long))
        {
            return ColumnType.Int64;
        }

        if (clrType == typeof(decimal))
        {
            return ColumnType.Decimal();
        }

        if (clrType == typeof(bool))
        {
            return ColumnType.Boolean;
        }

        if (clrType == typeof(Guid))
        {
            return ColumnType.Guid;
        }

        if (clrType == typeof(DateTime))
        {
            return ColumnType.DateTime;
        }

        if (clrType == typeof(DateTimeOffset))
        {
            return ColumnType.DateTimeOffset;
        }

        if (clrType == typeof(byte[]))
        {
            return ColumnType.Binary();
        }

        return ColumnType.String();
    }

    private static bool IsNullableType(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
    }

    private static Func<TEntity, object?> CreateGetter(PropertyInfo property)
    {
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var propertyAccess = Expression.Property(entity, property);
        var convert = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<TEntity, object?>>(convert, entity).Compile();
    }
}
