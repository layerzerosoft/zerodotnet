using System.Collections.Concurrent;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using LayerZero.Data.Internal.Registration;

namespace LayerZero.Data.Internal.Materialization;

internal interface IDataMaterializerSource
{
    Func<DbDataReader, TResult> GetMaterializer<TResult>(IReadOnlyList<string> aliases);
}

internal sealed class DataMaterializerSource(IEntityMapRegistry mapRegistry) : IDataMaterializerSource
{
    private readonly ConcurrentDictionary<string, Delegate> cache = new(StringComparer.Ordinal);

    public Func<DbDataReader, TResult> GetMaterializer<TResult>(IReadOnlyList<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        var key = $"{typeof(TResult).AssemblyQualifiedName}|{string.Join("|", aliases)}";
        return (Func<DbDataReader, TResult>)cache.GetOrAdd(key, static (_, state) => state.Build<TResult>(), this);
    }

    private Func<DbDataReader, TResult> Build<TResult>()
    {
        var aliases = default(IReadOnlyList<string>)!;
        return reader =>
        {
            aliases ??= GetAliases(reader);
            return (TResult)CreateMaterializedValue(typeof(TResult), aliases, reader)!;
        };
    }

    private object? CreateMaterializedValue(Type targetType, IReadOnlyList<string> aliases, DbDataReader reader)
    {
        if (IsSimpleType(targetType))
        {
            return ReadScalar(reader, ordinal: 0, targetType, column: null);
        }

        if (TryCreateJoinMaterializer(targetType, aliases, reader, out var joined))
        {
            return joined;
        }

        IEntityMap? map = null;
        Dictionary<string, IEntityColumn>? mapColumns = null;
        if (mapRegistry.TryGetMap(targetType, out map))
        {
            mapColumns = ((IEntityTable)map.Table).Columns
                .ToDictionary(static column => column.Name, static column => column, StringComparer.OrdinalIgnoreCase);
        }

        return CreateObject(targetType, aliases, reader, mapColumns);
    }

    private bool TryCreateJoinMaterializer(Type targetType, IReadOnlyList<string> aliases, DbDataReader reader, out object? result)
    {
        if (!targetType.IsGenericType || targetType.GetGenericTypeDefinition() != typeof(DataJoin<,>))
        {
            result = null;
            return false;
        }

        var genericArguments = targetType.GetGenericArguments();
        var leftType = genericArguments[0];
        var rightType = genericArguments[1];

        var leftAliases = aliases
            .Where(static alias => alias.StartsWith("l__", StringComparison.Ordinal))
            .Select(static alias => alias["l__".Length..])
            .ToArray();
        var rightAliases = aliases
            .Where(static alias => alias.StartsWith("r__", StringComparison.Ordinal))
            .Select(static alias => alias["r__".Length..])
            .ToArray();

        var leftValue = CreateMaterializedValue(leftType, leftAliases, new PrefixStrippingDataReader(reader, "l__"));
        var rightValue = CreateMaterializedValue(rightType, rightAliases, new PrefixStrippingDataReader(reader, "r__"));
        result = Activator.CreateInstance(targetType, leftValue, rightValue);
        return true;
    }

    private static object CreateObject(
        Type targetType,
        IReadOnlyList<string> aliases,
        DbDataReader reader,
        IReadOnlyDictionary<string, IEntityColumn>? mapColumns)
    {
        var sourceNames = aliases
            .Select(alias =>
            {
                if (mapColumns is not null && mapColumns.TryGetValue(alias, out var column))
                {
                    return column.PropertyName;
                }

                return alias;
            })
            .ToArray();

        var ordinalBySourceName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < sourceNames.Length; index++)
        {
            ordinalBySourceName[sourceNames[index]] = index;
        }

        var constructor = ChooseConstructor(targetType, ordinalBySourceName.Keys);
        if (constructor is null)
        {
            throw new InvalidOperationException($"No suitable constructor was found for materializing '{targetType.FullName}'.");
        }

        var parameters = constructor.GetParameters();
        var arguments = new object?[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            var ordinal = ordinalBySourceName[parameter.Name!];
            var column = ResolveMappedColumn(mapColumns, aliases[ordinal]);
            arguments[index] = ReadScalar(reader, ordinal, parameter.ParameterType, column);
        }

        var instance = constructor.Invoke(arguments);
        var assigned = new HashSet<string>(parameters.Select(static parameter => parameter.Name!), StringComparer.OrdinalIgnoreCase);

        foreach (var property in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || assigned.Contains(property.Name) || !ordinalBySourceName.TryGetValue(property.Name, out var ordinal))
            {
                continue;
            }

            var column = ResolveMappedColumn(mapColumns, aliases[ordinal]);
            property.SetValue(instance, ReadScalar(reader, ordinal, property.PropertyType, column));
        }

        return instance;
    }

    private static ConstructorInfo? ChooseConstructor(Type targetType, IEnumerable<string> sourceNames)
    {
        var sourceNameSet = new HashSet<string>(sourceNames, StringComparer.OrdinalIgnoreCase);

        return targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(static constructor => !constructor.IsStatic)
            .OrderByDescending(static constructor => constructor.GetParameters().Length)
            .FirstOrDefault(constructor =>
                constructor.GetParameters().All(parameter => sourceNameSet.Contains(parameter.Name!)))
            ?? targetType.GetConstructor(Type.EmptyTypes);
    }

    private static IEntityColumn? ResolveMappedColumn(IReadOnlyDictionary<string, IEntityColumn>? mapColumns, string alias)
    {
        if (mapColumns is null)
        {
            return null;
        }

        return mapColumns.TryGetValue(alias, out var column)
            ? column
            : null;
    }

    private static object? ReadScalar(DbDataReader reader, int ordinal, Type targetType, IEntityColumn? column)
    {
        if (reader.IsDBNull(ordinal))
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Activator.CreateInstance(targetType)
                : null;
        }

        var value = reader.GetValue(ordinal);
        if (column is not null)
        {
            value = ConvertMappedValue(column, value);
        }

        return ConvertValue(value, targetType);
    }

    private static object? ConvertMappedValue(IEntityColumn column, object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        return column.Converter?.ConvertFromProvider(value) ?? ConvertValue(value, column.ClrType);
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType.IsInstanceOfType(value))
        {
            return value;
        }

        if (effectiveType.IsEnum)
        {
            return value is string text
                ? Enum.Parse(effectiveType, text, ignoreCase: false)
                : Enum.ToObject(effectiveType, value);
        }

        if (effectiveType == typeof(Guid))
        {
            return value switch
            {
                Guid guid => guid,
                string text => Guid.Parse(text, CultureInfo.InvariantCulture),
                _ => new Guid((byte[])value),
            };
        }

        if (effectiveType == typeof(DateTimeOffset))
        {
            return value switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(dateTime),
                string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                _ => Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture),
            };
        }

        if (effectiveType == typeof(DateTime))
        {
            return value switch
            {
                DateTime dateTime => dateTime,
                DateTimeOffset offset => offset.UtcDateTime,
                string text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                _ => Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture),
            };
        }

        return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
    }

    private static bool IsSimpleType(Type targetType)
    {
        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return effectiveType.IsPrimitive
            || effectiveType.IsEnum
            || effectiveType == typeof(string)
            || effectiveType == typeof(decimal)
            || effectiveType == typeof(Guid)
            || effectiveType == typeof(DateTime)
            || effectiveType == typeof(DateTimeOffset)
            || effectiveType == typeof(byte[]);
    }

    private static IReadOnlyList<string> GetAliases(DbDataReader reader)
    {
        var aliases = new string[reader.FieldCount];
        for (var index = 0; index < reader.FieldCount; index++)
        {
            aliases[index] = reader.GetName(index);
        }

        return aliases;
    }
}

internal sealed class PrefixStrippingDataReader(DbDataReader inner, string prefix) : DbDataReader
{
    private readonly int[] ordinals = BuildOrdinals(inner, prefix);

    public override int FieldCount => ordinals.Length;

    public override bool Read() => throw new NotSupportedException();

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public override object GetValue(int ordinal) => inner.GetValue(ordinals[ordinal]);

    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinals[ordinal]);

    public override string GetName(int ordinal) => inner.GetName(ordinals[ordinal])[prefix.Length..];

    public override int GetOrdinal(string name)
    {
        for (var index = 0; index < ordinals.Length; index++)
        {
            if (string.Equals(GetName(index), name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        throw new IndexOutOfRangeException(name);
    }

    public override int Depth => inner.Depth;

    public override bool HasRows => inner.HasRows;

    public override bool IsClosed => inner.IsClosed;

    public override int RecordsAffected => inner.RecordsAffected;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool NextResult() => throw new NotSupportedException();

    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public override IEnumerator GetEnumerator() => throw new NotSupportedException();

    public override DataTable GetSchemaTable() => inner.GetSchemaTable()!;

    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinals[ordinal]);

    public override byte GetByte(int ordinal) => inner.GetByte(ordinals[ordinal]);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        inner.GetBytes(ordinals[ordinal], dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => inner.GetChar(ordinals[ordinal]);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        inner.GetChars(ordinals[ordinal], dataOffset, buffer, bufferOffset, length);

    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinals[ordinal]);

    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinals[ordinal]);

    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinals[ordinal]);

    public override double GetDouble(int ordinal) => inner.GetDouble(ordinals[ordinal]);

    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinals[ordinal]);

    public override float GetFloat(int ordinal) => inner.GetFloat(ordinals[ordinal]);

    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinals[ordinal]);

    public override short GetInt16(int ordinal) => inner.GetInt16(ordinals[ordinal]);

    public override int GetInt32(int ordinal) => inner.GetInt32(ordinals[ordinal]);

    public override long GetInt64(int ordinal) => inner.GetInt64(ordinals[ordinal]);

    public override string GetString(int ordinal) => inner.GetString(ordinals[ordinal]);

    public override int GetValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var length = Math.Min(values.Length, ordinals.Length);
        for (var index = 0; index < length; index++)
        {
            values[index] = GetValue(index);
        }

        return length;
    }

    private static int[] BuildOrdinals(DbDataReader reader, string prefix)
    {
        var matches = new List<int>(reader.FieldCount);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (reader.GetName(index).StartsWith(prefix, StringComparison.Ordinal))
            {
                matches.Add(index);
            }
        }

        return [.. matches];
    }
}
