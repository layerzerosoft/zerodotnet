using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using LayerZero.Data.Internal.Materialization;
using LayerZero.Data.Internal.Registration;
using LayerZero.Data.Internal.Sql;
using LayerZero.Data.Internal.Translation;

namespace LayerZero.Data.Internal.Execution;

internal sealed class DataContext(
    IEntityMapRegistry mapRegistry,
    IDatabaseConnectionFactory connectionFactory,
    IDataSqlDialect sqlDialect,
    DataScopeManager scopeManager,
    DataCommandCache commandCache,
    IDataMaterializerSource materializerSource) : IDataContext, IDataContextSession, IDataSqlContext
{
    public DataQuery<TEntity> Query<TEntity>()
        where TEntity : notnull =>
        new(this, DataQueryModel.Create<TEntity>());

    public ValueTask InsertAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : notnull
    {
        ArgumentNullException.ThrowIfNull(entity);

        var table = mapRegistry.GetTable<TEntity>();
        var key = DataCommandTranslation.CreateInsertCacheKey(table);
        var compiled = commandCache.GetOrAdd(key, () => sqlDialect.CompileInsert(DataCommandTranslation.CreateInsertTemplate(table)));
        var values = DataCommandTranslation.CollectInsertParameterValues(entity, table);
        return new ValueTask(ExecuteMutationAsync(compiled, values, cancellationToken).AsTask());
    }

    public DataUpdate<TEntity> Update<TEntity>()
        where TEntity : notnull =>
        new(this, DataUpdateModel.Create<TEntity>());

    public DataDelete<TEntity> Delete<TEntity>()
        where TEntity : notnull =>
        new(this, DataDeleteModel.Create<TEntity>());

    public async ValueTask<IDataScope> BeginScopeAsync(CancellationToken cancellationToken = default)
    {
        if (scopeManager.Current is not null)
        {
            return JoinedDataScope.Instance;
        }

        var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var activeScope = new ActiveDataScope(connection, transaction);
        scopeManager.Current = activeScope;
        return new DataScope(scopeManager, activeScope);
    }

    public IDataSqlContext Sql() => this;

    public ValueTask<IReadOnlyList<TResult>> ListAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken) =>
        ExecuteReaderListAsync<TResult>(model, projection, DataReadMode.List, cancellationToken);

    public async ValueTask<TResult> FirstAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken)
    {
        var results = await ExecuteReaderListAsync<TResult>(model, projection, DataReadMode.First, cancellationToken).ConfigureAwait(false);
        return results.Count == 0
            ? throw new InvalidOperationException("The query returned no rows.")
            : results[0];
    }

    public async ValueTask<TResult?> FirstOrDefaultAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken)
    {
        var results = await ExecuteReaderListAsync<TResult>(model, projection, DataReadMode.FirstOrDefault, cancellationToken).ConfigureAwait(false);
        return results.Count == 0
            ? default
            : results[0];
    }

    public async ValueTask<TResult> SingleAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken)
    {
        var results = await ExecuteReaderListAsync<TResult>(model, projection, DataReadMode.Single, cancellationToken).ConfigureAwait(false);
        return results.Count switch
        {
            0 => throw new InvalidOperationException("The query returned no rows."),
            > 1 => throw new InvalidOperationException("The query returned more than one row."),
            _ => results[0],
        };
    }

    public async ValueTask<TResult?> SingleOrDefaultAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        CancellationToken cancellationToken)
    {
        var results = await ExecuteReaderListAsync<TResult>(model, projection, DataReadMode.SingleOrDefault, cancellationToken).ConfigureAwait(false);
        return results.Count switch
        {
            0 => default,
            > 1 => throw new InvalidOperationException("The query returned more than one row."),
            _ => results[0],
        };
    }

    public async ValueTask<TResult> AggregateAsync<TRow, TResult>(
        DataQueryModel model,
        DataAggregateKind aggregate,
        LambdaExpression? selector,
        CancellationToken cancellationToken)
    {
        var key = DataCommandTranslation.CreateAggregateCacheKey(model, aggregate, selector, typeof(TResult));
        var compiled = commandCache.GetOrAdd(key, () => sqlDialect.CompileAggregate(
            DataCommandTranslation.CreateAggregateTemplate<TResult>(model, aggregate, selector, mapRegistry)));
        var values = DataCommandTranslation.CollectAggregateParameterValues(model, selector);
        var result = await ExecuteScalarAsync(compiled, values, useTransaction: false, cancellationToken).ConfigureAwait(false);
        if (result is null || result is DBNull)
        {
            return aggregate switch
            {
                DataAggregateKind.Any => default!,
                _ => default!,
            };
        }

        return (TResult)ConvertScalar(result, typeof(TResult))!;
    }

    public ValueTask<int> ExecuteUpdateAsync<TEntity>(DataUpdateModel model, CancellationToken cancellationToken)
        where TEntity : notnull =>
        ExecuteUpdateCoreAsync(model, cancellationToken);

    public ValueTask<int> ExecuteDeleteAsync<TEntity>(DataDeleteModel model, CancellationToken cancellationToken)
        where TEntity : notnull =>
        ExecuteDeleteCoreAsync(model, cancellationToken);

    public ValueTask<int> ExecuteAsync(DataSqlStatement statement, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement.CommandText);
        var compiled = sqlDialect.CompileRawSql(statement);
        return ExecuteMutationAsync(compiled, statement.Parameters.Select(static parameter => parameter.Value).ToArray(), cancellationToken);
    }

    public ValueTask<IReadOnlyList<TResult>> ListAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement.CommandText);
        return ExecuteRawReaderListAsync<TResult>(statement, DataReadMode.List, cancellationToken);
    }

    public async ValueTask<TResult> FirstAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default)
    {
        var results = await ExecuteRawReaderListAsync<TResult>(statement, DataReadMode.First, cancellationToken).ConfigureAwait(false);
        return results.Count == 0
            ? throw new InvalidOperationException("The SQL query returned no rows.")
            : results[0];
    }

    public async ValueTask<TResult?> FirstOrDefaultAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default)
    {
        var results = await ExecuteRawReaderListAsync<TResult>(statement, DataReadMode.FirstOrDefault, cancellationToken).ConfigureAwait(false);
        return results.Count == 0
            ? default
            : results[0];
    }

    public async ValueTask<TResult> SingleAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default)
    {
        var results = await ExecuteRawReaderListAsync<TResult>(statement, DataReadMode.Single, cancellationToken).ConfigureAwait(false);
        return results.Count switch
        {
            0 => throw new InvalidOperationException("The SQL query returned no rows."),
            > 1 => throw new InvalidOperationException("The SQL query returned more than one row."),
            _ => results[0],
        };
    }

    public async ValueTask<TResult?> SingleOrDefaultAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default)
    {
        var results = await ExecuteRawReaderListAsync<TResult>(statement, DataReadMode.SingleOrDefault, cancellationToken).ConfigureAwait(false);
        return results.Count switch
        {
            0 => default,
            > 1 => throw new InvalidOperationException("The SQL query returned more than one row."),
            _ => results[0],
        };
    }

    private async ValueTask<IReadOnlyList<TResult>> ExecuteReaderListAsync<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        DataReadMode readMode,
        CancellationToken cancellationToken)
    {
        var key = DataCommandTranslation.CreateReaderCacheKey(model, projection, readMode, typeof(TResult));
        var compiled = commandCache.GetOrAdd(key, () => sqlDialect.CompileReader(
            DataCommandTranslation.CreateReaderTemplate<TResult>(model, projection, mapRegistry),
            readMode));
        var values = DataCommandTranslation.CollectReaderParameterValues(model, projection);
        return await ExecuteReaderAsync<TResult>(
            compiled,
            values,
            useTransaction: false,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> ExecuteUpdateCoreAsync(DataUpdateModel model, CancellationToken cancellationToken)
    {
        var key = DataCommandTranslation.CreateUpdateCacheKey(model);
        var compiled = commandCache.GetOrAdd(key, () => sqlDialect.CompileUpdate(
            DataCommandTranslation.CreateUpdateTemplate(model, mapRegistry)));
        var values = DataCommandTranslation.CollectUpdateParameterValues(model);
        return await ExecuteMutationAsync(compiled, values, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> ExecuteDeleteCoreAsync(DataDeleteModel model, CancellationToken cancellationToken)
    {
        var key = DataCommandTranslation.CreateDeleteCacheKey(model);
        var compiled = commandCache.GetOrAdd(key, () => sqlDialect.CompileDelete(
            DataCommandTranslation.CreateDeleteTemplate(model, mapRegistry)));
        var values = DataCommandTranslation.CollectDeleteParameterValues(model);
        return await ExecuteMutationAsync(compiled, values, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<TResult>> ExecuteRawReaderListAsync<TResult>(
        DataSqlStatement statement,
        DataReadMode readMode,
        CancellationToken cancellationToken)
    {
        var compiled = sqlDialect.CompileRawSql(statement);
        var values = statement.Parameters.Select(static parameter => parameter.Value).ToArray();
        return await ExecuteReaderAsync<TResult>(
            compiled,
            values,
            useTransaction: false,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> ExecuteMutationAsync(
        CompiledDataCommandTemplate compiled,
        object?[] parameterValues,
        CancellationToken cancellationToken)
    {
        if (scopeManager.Current is { } activeScope)
        {
            await using var command = CreateCommand(compiled, parameterValues, activeScope.Connection, activeScope.Transaction);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = CreateCommand(compiled, parameterValues, connection, transaction);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return affected;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<object?> ExecuteScalarAsync(
        CompiledDataCommandTemplate compiled,
        object?[] parameterValues,
        bool useTransaction,
        CancellationToken cancellationToken)
    {
        if (scopeManager.Current is { } activeScope)
        {
            await using var scopedCommand = CreateCommand(compiled, parameterValues, activeScope.Connection, useTransaction ? activeScope.Transaction : null);
            return await scopedCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var scalarCommand = CreateCommand(compiled, parameterValues, connection, transaction: null);
        return await scalarCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<TResult>> ExecuteReaderAsync<TResult>(
        CompiledDataCommandTemplate compiled,
        object?[] parameterValues,
        bool useTransaction,
        CancellationToken cancellationToken)
    {
        if (scopeManager.Current is { } activeScope)
        {
            return await ExecuteReaderWithConnectionAsync<TResult>(
                compiled,
                parameterValues,
                activeScope.Connection,
                useTransaction ? activeScope.Transaction : null,
                cancellationToken).ConfigureAwait(false);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await ExecuteReaderWithConnectionAsync<TResult>(
            compiled,
            parameterValues,
            connection,
            transaction: null,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<TResult>> ExecuteReaderWithConnectionAsync<TResult>(
        CompiledDataCommandTemplate compiled,
        object?[] parameterValues,
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(compiled, parameterValues, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var aliases = compiled.ResultAliases.Count == 0
            ? GetAliases(reader)
            : compiled.ResultAliases;
        var materializer = materializerSource.GetMaterializer<TResult>(aliases);
        var rows = new List<TResult>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(materializer(reader));
        }

        return rows;
    }

    private static DbCommand CreateCommand(
        CompiledDataCommandTemplate compiled,
        IReadOnlyList<object?> parameterValues,
        DbConnection connection,
        DbTransaction? transaction)
    {
        var command = connection.CreateCommand();
        command.CommandText = compiled.CommandText;
        command.Transaction = transaction;

        for (var index = 0; index < compiled.Parameters.Count; index++)
        {
            var descriptor = compiled.Parameters[index];
            var parameter = command.CreateParameter();
            parameter.ParameterName = descriptor.Name;
            parameter.Value = parameterValues[index] ?? DBNull.Value;
            if (TryGetDbType(descriptor.ValueType, out var dbType))
            {
                parameter.DbType = dbType;
            }

            command.Parameters.Add(parameter);
        }

        return command;
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

    private static object? ConvertScalar(object value, Type targetType)
    {
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
            return value is Guid guid
                ? guid
                : Guid.Parse(value.ToString()!, CultureInfo.InvariantCulture);
        }

        if (effectiveType == typeof(DateTimeOffset))
        {
            return value switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(dateTime),
                _ => DateTimeOffset.Parse(value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            };
        }

        if (effectiveType == typeof(DateTime))
        {
            return value switch
            {
                DateTime dateTime => dateTime,
                DateTimeOffset offset => offset.UtcDateTime,
                _ => DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            };
        }

        return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
    }

    private static bool TryGetDbType(Type type, out DbType dbType)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (effectiveType.IsEnum)
        {
            effectiveType = Enum.GetUnderlyingType(effectiveType);
        }

        switch (Type.GetTypeCode(effectiveType))
        {
            case TypeCode.Boolean:
                dbType = DbType.Boolean;
                return true;
            case TypeCode.Byte:
                dbType = DbType.Byte;
                return true;
            case TypeCode.Int16:
                dbType = DbType.Int16;
                return true;
            case TypeCode.Int32:
                dbType = DbType.Int32;
                return true;
            case TypeCode.Int64:
                dbType = DbType.Int64;
                return true;
            case TypeCode.Decimal:
                dbType = DbType.Decimal;
                return true;
            case TypeCode.Double:
                dbType = DbType.Double;
                return true;
            case TypeCode.Single:
                dbType = DbType.Single;
                return true;
            case TypeCode.DateTime:
                dbType = DbType.DateTime2;
                return true;
            case TypeCode.String:
                dbType = DbType.String;
                return true;
        }

        if (effectiveType == typeof(Guid))
        {
            dbType = DbType.Guid;
            return true;
        }

        if (effectiveType == typeof(DateTimeOffset))
        {
            dbType = DbType.DateTimeOffset;
            return true;
        }

        if (effectiveType == typeof(byte[]))
        {
            dbType = DbType.Binary;
            return true;
        }

        dbType = default;
        return false;
    }
}
