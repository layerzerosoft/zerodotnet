using System.Buffers.Binary;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LayerZero.Data;
using LayerZero.Data.Postgres.Configuration;
using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using LayerZero.Migrations.Postgres.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LayerZero.Migrations.Postgres.Internal;

internal sealed class PostgresMigrationDatabaseAdapter(
    IDatabaseConnectionFactory connectionFactory,
    IOptions<PostgresDataOptions> dataOptionsAccessor,
    IOptions<PostgresMigrationsOptions> optionsAccessor) : IMigrationDatabaseAdapter
{
    private readonly IDatabaseConnectionFactory connectionFactory = connectionFactory;
    private readonly PostgresDataOptions dataOptions = dataOptionsAccessor.Value;
    private readonly PostgresMigrationsOptions options = optionsAccessor.Value;

    public async ValueTask<MigrationDatabaseSnapshot> ReadStateAsync(MigrationsOptions options, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var historyExists = await HistoryTableExistsAsync(connection, options, cancellationToken).ConfigureAwait(false);
        var hasUserObjects = await HasUserObjectsAsync(connection, options, cancellationToken).ConfigureAwait(false);
        var appliedArtifacts = historyExists
            ? await ReadAppliedArtifactsAsync(connection, options, cancellationToken).ConfigureAwait(false)
            : [];
        return new MigrationDatabaseSnapshot(historyExists, hasUserObjects, appliedArtifacts);
    }

    public async ValueTask EnsureHistoryStoreAsync(MigrationsOptions options, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, transaction: null, RenderEnsureHistoryStore(options), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IAsyncDisposable> AcquireLockAsync(MigrationsOptions options, CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var lockKey = ComputeLockKey(options.LockName);

        try
        {
            var started = DateTimeOffset.UtcNow;
            while (!await TryAcquireLockAsync(connection, lockKey, cancellationToken).ConfigureAwait(false))
            {
                if (DateTimeOffset.UtcNow - started >= this.options.LockTimeout)
                {
                    throw new InvalidOperationException(
                        $"PostgreSQL could not acquire the LayerZero migration lock within {this.options.LockTimeout}.");
                }

                var remaining = this.options.LockTimeout - (DateTimeOffset.UtcNow - started);
                var delay = remaining <= TimeSpan.Zero
                    ? TimeSpan.Zero
                    : remaining < TimeSpan.FromMilliseconds(100)
                        ? remaining
                        : TimeSpan.FromMilliseconds(100);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            return new PostgresMigrationLock(connection, lockKey, this.options.CommandTimeoutSeconds);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public string GenerateScript(
        MigrationExecutionMode mode,
        MigrationsOptions options,
        string executor,
        IReadOnlyList<CompiledArtifact> artifacts)
    {
        if (artifacts.Count == 0)
        {
            return "-- No pending LayerZero migration artifacts.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("-- LayerZero PostgreSQL migrations");
        builder.AppendLine(RenderEnsureHistoryStore(options));
        builder.AppendLine();
        builder.AppendLine(RenderLockScript(options));

        for (var index = 0; index < artifacts.Count; index++)
        {
            var artifact = artifacts[index];
            builder.AppendLine();
            builder.AppendLine($"-- {mode}: {artifact.Kind}:{artifact.Profile}:{artifact.Id} {artifact.Name}");
            builder.AppendLine(RenderArtifactBatch(mode, options, executor, artifact, index));
        }

        return builder.ToString();
    }

    public async ValueTask ExecuteAsync(
        MigrationExecutionMode mode,
        MigrationsOptions options,
        string executor,
        IReadOnlyList<CompiledArtifact> artifacts,
        CancellationToken cancellationToken)
    {
        if (artifacts.Count == 0)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < artifacts.Count; index++)
        {
            var artifact = artifacts[index];
            var sql = RenderArtifactBody(mode, artifact, index);
            var historyInsert = RenderHistoryInsert(options, executor, artifact);

            if (mode == MigrationExecutionMode.Apply && artifact.TransactionMode == MigrationTransactionMode.Transactional)
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (!string.IsNullOrWhiteSpace(sql))
                    {
                        await ExecuteNonQueryAsync(connection, transaction, sql, cancellationToken).ConfigureAwait(false);
                    }

                    await ExecuteNonQueryAsync(connection, transaction, historyInsert, cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(sql))
            {
                await ExecuteNonQueryAsync(connection, transaction: null, sql, cancellationToken).ConfigureAwait(false);
            }

            await ExecuteNonQueryAsync(connection, transaction: null, historyInsert, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return (NpgsqlConnection)connection;
    }

    private async ValueTask<bool> HistoryTableExistsAsync(
        NpgsqlConnection connection,
        MigrationsOptions runtimeOptions,
        CancellationToken cancellationToken)
    {
        var historySchema = GetHistorySchema(runtimeOptions);

        await using var command = connection.CreateCommand();
        command.CommandText = "select to_regclass($1) is not null;";
        command.Parameters.AddWithValue(FormatRegClassLiteral(historySchema, runtimeOptions.HistoryTableName));
        ApplyCommandTimeout(command);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private async ValueTask<bool> HasUserObjectsAsync(
        NpgsqlConnection connection,
        MigrationsOptions runtimeOptions,
        CancellationToken cancellationToken)
    {
        var historySchema = GetHistorySchema(runtimeOptions);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists (
                select 1
                from pg_catalog.pg_class c
                inner join pg_catalog.pg_namespace n on n.oid = c.relnamespace
                where c.relkind in ('r', 'p')
                  and n.nspname not in ('pg_catalog', 'information_schema')
                  and n.nspname not like 'pg_toast%'
                  and not (n.nspname = $1 and c.relname = $2)
            );
            """;
        command.Parameters.AddWithValue(historySchema);
        command.Parameters.AddWithValue(runtimeOptions.HistoryTableName);
        ApplyCommandTimeout(command);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private async ValueTask<IReadOnlyList<AppliedArtifactRecord>> ReadAppliedArtifactsAsync(
        NpgsqlConnection connection,
        MigrationsOptions runtimeOptions,
        CancellationToken cancellationToken)
    {
        var records = new List<AppliedArtifactRecord>();
        var historySchema = GetHistorySchema(runtimeOptions);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select artifact_kind, artifact_id, artifact_profile, artifact_name, checksum, applied_utc, executor
            from {FormatTable(historySchema, runtimeOptions.HistoryTableName)}
            order by artifact_kind asc, artifact_profile asc, artifact_id asc;
            """;
        ApplyCommandTimeout(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(new AppliedArtifactRecord(
                ParseArtifactKind(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetString(6)));
        }

        return records;
    }

    private string RenderEnsureHistoryStore(MigrationsOptions runtimeOptions)
    {
        var historySchema = GetHistorySchema(runtimeOptions);
        var historyTable = FormatTable(historySchema, runtimeOptions.HistoryTableName);
        return $"""
            create schema if not exists {QuoteIdentifier(historySchema)};

            create table if not exists {historyTable}(
                artifact_kind character varying(16) not null,
                artifact_id character(14) not null,
                artifact_profile character varying(128) not null default '',
                artifact_name character varying(256) not null,
                checksum character(64) not null,
                applied_utc timestamp with time zone not null,
                executor character varying(256) not null,
                constraint {QuoteIdentifier($"PK_{runtimeOptions.HistoryTableName}")} primary key (artifact_kind, artifact_profile, artifact_id)
            );
            """;
    }

    private string RenderLockScript(MigrationsOptions runtimeOptions)
    {
        return $"select pg_advisory_lock({ComputeLockKey(runtimeOptions.LockName).ToString(CultureInfo.InvariantCulture)});";
    }

    private string RenderArtifactBatch(
        MigrationExecutionMode mode,
        MigrationsOptions runtimeOptions,
        string executor,
        CompiledArtifact artifact,
        int artifactIndex)
    {
        var body = RenderArtifactBody(mode, artifact, artifactIndex);
        var historyInsert = RenderHistoryInsert(runtimeOptions, executor, artifact);

        if (mode == MigrationExecutionMode.Apply && artifact.TransactionMode == MigrationTransactionMode.Transactional)
        {
            return $"""
                begin;
                {body}
                {historyInsert}
                commit;
                """;
        }

        return string.IsNullOrWhiteSpace(body)
            ? historyInsert
            : $"""
                {body}
                {historyInsert}
                """;
    }

    private string RenderArtifactBody(MigrationExecutionMode mode, CompiledArtifact artifact, int artifactIndex)
    {
        if (mode == MigrationExecutionMode.Baseline)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var operationIndex = 0; operationIndex < artifact.Operations.Count; operationIndex++)
        {
            builder.AppendLine(RenderOperation(artifact, artifact.Operations[operationIndex], operationIndex));
        }

        return builder.ToString();
    }

    private string RenderHistoryInsert(MigrationsOptions runtimeOptions, string executor, CompiledArtifact artifact)
    {
        return $"""
            insert into {FormatTable(GetHistorySchema(runtimeOptions), runtimeOptions.HistoryTableName)}(artifact_kind, artifact_id, artifact_profile, artifact_name, checksum, applied_utc, executor)
            values (
                {ToSqlLiteral(artifact.Kind == MigrationArtifactKind.Migration ? "migration" : "seed")},
                {ToSqlLiteral(artifact.Id)},
                {ToSqlLiteral(artifact.HistoryProfile)},
                {ToSqlLiteral(artifact.Name)},
                {ToSqlLiteral(artifact.Checksum)},
                current_timestamp,
                {ToSqlLiteral(executor)}
            );
            """;
    }

    private string GetHistorySchema(MigrationsOptions runtimeOptions) =>
        string.IsNullOrWhiteSpace(runtimeOptions.HistoryTableSchema)
            ? dataOptions.DefaultSchema
            : runtimeOptions.HistoryTableSchema;

    private string RenderOperation(CompiledArtifact artifact, RelationalOperation operation, int operationIndex)
    {
        return operation switch
        {
            EnsureSchemaOperation value => $"""create schema if not exists {QuoteIdentifier(value.Schema)};""",
            CreateTableOperation value => RenderCreateTable(value),
            DropTableOperation value => $"""drop table if exists {FormatTable(value.Table)};""",
            AddColumnOperation value => $"""alter table {FormatTable(value.Table)} add column if not exists {RenderColumn(value.Column)};""",
            CreateIndexOperation value => $"""create {(value.IsUnique ? "unique " : string.Empty)}index if not exists {QuoteIdentifier(value.Name)} on {FormatTable(value.Table)}({string.Join(", ", value.Columns.Select(QuoteIdentifier))});""",
            DropIndexOperation value => $"""drop index if exists {FormatIndex(value.Table, value.Name)};""",
            InsertDataOperation value => string.Join(Environment.NewLine, value.Rows.Select(row => RenderInsert(value.Table, row))),
            UpdateDataOperation value => RenderUpdate(value.Table, value.Key, value.Values),
            DeleteDataOperation value => RenderDelete(value.Table, value.Key),
            UpsertDataOperation value => RenderUpsert(value.Table, value.KeyColumns, value.Values),
            SyncDataOperation value => RenderSync(artifact, value, operationIndex),
            SqlOperation value => value.Sql.EndsWith(';') ? value.Sql : value.Sql + ";",
            _ => throw new InvalidOperationException($"Unsupported PostgreSQL migration operation '{operation.GetType().FullName}'."),
        };
    }

    private string RenderCreateTable(CreateTableOperation operation)
    {
        var columnDefinitions = operation.Columns.Select(RenderColumn).ToList();
        columnDefinitions.Add($"primary key ({string.Join(", ", operation.PrimaryKeyColumns.Select(QuoteIdentifier))})");

        return $"""
            create table if not exists {FormatTable(operation.Table)}(
                {string.Join("," + Environment.NewLine + "    ", columnDefinitions)}
            );
            """;
    }

    private string RenderInsert(QualifiedTableName table, ColumnValueSet row)
    {
        var columns = row.Values.Keys.Select(QuoteIdentifier).ToArray();
        var values = row.Values.Values.Select(ToSqlLiteral).ToArray();
        return $"""
            insert into {FormatTable(table)}({string.Join(", ", columns)})
            values ({string.Join(", ", values)});
            """;
    }

    private string RenderUpdate(QualifiedTableName table, ColumnValueSet key, ColumnValueSet values)
    {
        return $"""
            update {FormatTable(table)}
            set {string.Join(", ", values.Values.Select(pair => $"{QuoteIdentifier(pair.Key)} = {ToSqlLiteral(pair.Value)}"))}
            where {RenderPredicate(key)};
            """;
    }

    private string RenderDelete(QualifiedTableName table, ColumnValueSet key)
    {
        return $"""
            delete from {FormatTable(table)}
            where {RenderPredicate(key)};
            """;
    }

    private string RenderUpsert(QualifiedTableName table, IReadOnlyList<string> keyColumns, ColumnValueSet values)
    {
        var columns = values.Values.Keys.Select(QuoteIdentifier).ToArray();
        var insertValues = values.Values.Values.Select(ToSqlLiteral).ToArray();
        var nonKeyColumns = values.Values.Keys
            .Where(column => !keyColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var conflictTarget = string.Join(", ", keyColumns.Select(QuoteIdentifier));
        if (nonKeyColumns.Length == 0)
        {
            return $"""
                insert into {FormatTable(table)}({string.Join(", ", columns)})
                values ({string.Join(", ", insertValues)})
                on conflict ({conflictTarget}) do nothing;
                """;
        }

        return $"""
            insert into {FormatTable(table)}({string.Join(", ", columns)})
            values ({string.Join(", ", insertValues)})
            on conflict ({conflictTarget}) do update
            set {string.Join(", ", nonKeyColumns.Select(column => $"{QuoteIdentifier(column)} = excluded.{QuoteIdentifier(column)}"))};
            """;
    }

    private string RenderSync(CompiledArtifact artifact, SyncDataOperation operation, int operationIndex)
    {
        var columns = operation.Rows
            .SelectMany(static row => row.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var nonKeyColumns = columns
            .Where(column => !operation.KeyColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var tempTableName = $"lz_sync_{artifact.Id}_{operationIndex.ToString(CultureInfo.InvariantCulture)}";
        var tempTableColumns = columns
            .Select(column => $"{QuoteIdentifier(column)} {InferSqlType(operation.Rows, column)} null")
            .ToArray();
        var inserts = operation.Rows.Select(row => $"""
            insert into {QuoteIdentifier(tempTableName)}({string.Join(", ", columns.Select(QuoteIdentifier))})
            values ({string.Join(", ", columns.Select(column => row.Values.TryGetValue(column, out var value) ? ToSqlLiteral(value) : "null"))});
            """);

        var keyPredicate = string.Join(
            " and ",
            operation.KeyColumns.Select(column => $"target.{QuoteIdentifier(column)} is not distinct from source.{QuoteIdentifier(column)}"));
        var insertPredicate = string.Join(
            " and ",
            operation.KeyColumns.Select(column => $"existing.{QuoteIdentifier(column)} is not distinct from source.{QuoteIdentifier(column)}"));
        var deletePredicate = string.Join(
            " and ",
            operation.KeyColumns.Select(column => $"source.{QuoteIdentifier(column)} is not distinct from target.{QuoteIdentifier(column)}"));

        var updateStatement = nonKeyColumns.Length == 0
            ? string.Empty
            : $"""
                update {FormatTable(operation.Table)} as target
                set {string.Join(", ", nonKeyColumns.Select(column => $"{QuoteIdentifier(column)} = source.{QuoteIdentifier(column)}"))}
                from {QuoteIdentifier(tempTableName)} as source
                where {keyPredicate};
                """;

        return $"""
            create temporary table {QuoteIdentifier(tempTableName)}(
                {string.Join("," + Environment.NewLine + "    ", tempTableColumns)}
            );

            {string.Join(Environment.NewLine, inserts)}

            {updateStatement}

            insert into {FormatTable(operation.Table)}({string.Join(", ", columns.Select(QuoteIdentifier))})
            select {string.Join(", ", columns.Select(column => $"source.{QuoteIdentifier(column)}"))}
            from {QuoteIdentifier(tempTableName)} as source
            where not exists (
                select 1
                from {FormatTable(operation.Table)} as existing
                where {insertPredicate}
            );

            delete from {FormatTable(operation.Table)} as target
            where not exists (
                select 1
                from {QuoteIdentifier(tempTableName)} as source
                where {deletePredicate}
            );

            drop table if exists {QuoteIdentifier(tempTableName)};
            """;
    }

    private string RenderPredicate(ColumnValueSet key)
    {
        return string.Join(
            " and ",
            key.Values.Select(pair => $"{QuoteIdentifier(pair.Key)} is not distinct from {ToSqlLiteral(pair.Value)}"));
    }

    private string RenderColumn(ColumnDefinition column)
    {
        var builder = new StringBuilder();
        builder.Append(QuoteIdentifier(column.Name))
            .Append(' ')
            .Append(ToSqlType(column.Type));

        if (column.IsIdentity)
        {
            builder.Append(" generated by default as identity");
        }

        builder.Append(column.IsNullable ? " null" : " not null");

        if (column.DefaultValue is not null)
        {
            builder.Append(" default ").Append(ToSqlLiteral(column.DefaultValue));
        }

        return builder.ToString();
    }

    private string ToSqlType(ColumnType type)
    {
        return type.Kind switch
        {
            RelationalTypeKind.Int32 => "integer",
            RelationalTypeKind.Int64 => "bigint",
            RelationalTypeKind.Decimal => $"numeric({type.Precision ?? 18}, {type.Scale ?? 2})",
            RelationalTypeKind.String => type.Length is null ? "text" : $"character varying({type.Length.Value})",
            RelationalTypeKind.Boolean => "boolean",
            RelationalTypeKind.Guid => "uuid",
            RelationalTypeKind.DateTime => "timestamp without time zone",
            RelationalTypeKind.DateTimeOffset => "timestamp with time zone",
            RelationalTypeKind.Binary => "bytea",
            _ => throw new InvalidOperationException($"Unsupported relational type '{type.Kind}'."),
        };
    }

    private string InferSqlType(IReadOnlyList<ColumnValueSet> rows, string column)
    {
        var sample = rows
            .Select(row => row.Values.TryGetValue(column, out var value) ? value : null)
            .FirstOrDefault(static value => value is not null);

        return sample switch
        {
            null => "text",
            int => "integer",
            long => "bigint",
            short => "smallint",
            byte => "smallint",
            decimal => "numeric(38, 18)",
            double => "double precision",
            float => "real",
            bool => "boolean",
            Guid => "uuid",
            DateTime => "timestamp without time zone",
            DateTimeOffset => "timestamp with time zone",
            byte[] => "bytea",
            string => "text",
            _ => "text",
        };
    }

    private string FormatTable(QualifiedTableName table) => FormatTable(ResolveSchema(table.Schema), table.Name);

    private string FormatTable(string schema, string tableName) => $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";

    private string FormatIndex(QualifiedTableName table, string indexName) => $"{QuoteIdentifier(ResolveSchema(table.Schema))}.{QuoteIdentifier(indexName)}";

    private string ResolveSchema(string? schema) => string.IsNullOrWhiteSpace(schema) ? dataOptions.DefaultSchema : schema;

    private static string FormatRegClassLiteral(string schema, string tableName) => $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string ToSqlLiteral(object? value)
    {
        return value switch
        {
            null => "null",
            string text => ToSqlString(text),
            bool flag => flag ? "true" : "false",
            byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture),
            Guid guidValue => $"{ToSqlString(guidValue.ToString("D", CultureInfo.InvariantCulture))}::uuid",
            DateTime dateTimeValue => $"{ToSqlString(dateTimeValue.ToString("O", CultureInfo.InvariantCulture))}::timestamp",
            DateTimeOffset dateTimeOffsetValue => $"{ToSqlString(dateTimeOffsetValue.ToString("O", CultureInfo.InvariantCulture))}::timestamptz",
            byte[] bytes => $"decode('{Convert.ToHexString(bytes)}', 'hex')",
            _ when value is IFormattable formattable => ToSqlString(formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty),
            _ => ToSqlString(value.ToString() ?? string.Empty),
        };
    }

    private static string ToSqlString(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static MigrationArtifactKind ParseArtifactKind(string value)
    {
        return value switch
        {
            "migration" => MigrationArtifactKind.Migration,
            "seed" => MigrationArtifactKind.Seed,
            _ => throw new InvalidOperationException($"Unsupported history artifact kind '{value}'."),
        };
    }

    private void ApplyCommandTimeout(NpgsqlCommand command)
    {
        if (options.CommandTimeoutSeconds is { } timeout)
        {
            command.CommandTimeout = timeout;
        }
    }

    private async ValueTask<bool> TryAcquireLockAsync(
        NpgsqlConnection connection,
        long lockKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select pg_try_advisory_lock($1);";
        command.Parameters.AddWithValue(lockKey);
        ApplyCommandTimeout(command);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private async ValueTask ExecuteNonQueryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        ApplyCommandTimeout(command);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static long ComputeLockKey(string lockName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(lockName));
        return BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(0, sizeof(long)));
    }
}

internal sealed class PostgresMigrationLock(
    NpgsqlConnection connection,
    long lockKey,
    int? commandTimeoutSeconds) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (connection.State == ConnectionState.Open)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "select pg_advisory_unlock($1);";
                command.Parameters.AddWithValue(lockKey);

                if (commandTimeoutSeconds is { } timeout)
                {
                    command.CommandTimeout = timeout;
                }

                await command.ExecuteScalarAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
