using System.Data;
using System.Globalization;
using System.Text;
using LayerZero.Data;
using LayerZero.Data.SqlServer.Configuration;
using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using LayerZero.Migrations.SqlServer.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace LayerZero.Migrations.SqlServer.Internal;

internal sealed class SqlServerMigrationDatabaseAdapter(
    IDatabaseConnectionFactory connectionFactory,
    IOptions<SqlServerDataOptions> dataOptionsAccessor,
    IOptions<SqlServerMigrationsOptions> optionsAccessor) : IMigrationDatabaseAdapter
{
    private readonly IDatabaseConnectionFactory connectionFactory = connectionFactory;
    private readonly SqlServerDataOptions dataOptions = dataOptionsAccessor.Value;
    private readonly SqlServerMigrationsOptions options = optionsAccessor.Value;

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

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                declare @result int;
                exec @result = sys.sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Session',
                    @LockTimeout = @timeout;
                select @result;
                """;
            command.Parameters.AddWithValue("@resource", options.LockName);
            command.Parameters.AddWithValue("@timeout", GetLockTimeoutMilliseconds());
            ApplyCommandTimeout(command);

            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (result < 0)
            {
                throw new InvalidOperationException(
                    $"SQL Server could not acquire the LayerZero migration lock within {this.options.LockTimeout}.");
            }

            return new SqlServerMigrationLock(connection, options.LockName, this.options.CommandTimeoutSeconds);
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
        builder.AppendLine("-- LayerZero SQL Server migrations");
        builder.AppendLine(RenderEnsureHistoryStore(options));
        builder.AppendLine("go");
        builder.AppendLine();
        builder.AppendLine(RenderLockScript(options));
        builder.AppendLine("go");

        for (var index = 0; index < artifacts.Count; index++)
        {
            var artifact = artifacts[index];
            builder.AppendLine();
            builder.AppendLine($"-- {mode}: {artifact.Kind}:{artifact.Profile}:{artifact.Id} {artifact.Name}");
            builder.AppendLine(RenderArtifactBatch(mode, options, executor, artifact, index));
            builder.AppendLine("go");
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
        await ExecuteNonQueryAsync(connection, transaction: null, "set xact_abort on;", cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < artifacts.Count; index++)
        {
            var artifact = artifacts[index];
            var sql = RenderArtifactBody(mode, artifact, index);
            var historyInsert = RenderHistoryInsert(options, executor, artifact);

            if (mode == MigrationExecutionMode.Apply && artifact.TransactionMode == MigrationTransactionMode.Transactional)
            {
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
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

    private async ValueTask<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return (SqlConnection)connection;
    }

    private async ValueTask<bool> HistoryTableExistsAsync(
        SqlConnection connection,
        MigrationsOptions runtimeOptions,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select case when exists (
                select 1
                from sys.tables t
                inner join sys.schemas s on s.schema_id = t.schema_id
                where s.name = @schema and t.name = @name
            ) then 1 else 0 end;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@schema", runtimeOptions.HistoryTableSchema);
        command.Parameters.AddWithValue("@name", runtimeOptions.HistoryTableName);
        ApplyCommandTimeout(command);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;
    }

    private async ValueTask<bool> HasUserObjectsAsync(
        SqlConnection connection,
        MigrationsOptions runtimeOptions,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select case when exists (
                select 1
                from sys.tables t
                inner join sys.schemas s on s.schema_id = t.schema_id
                where t.is_ms_shipped = 0
                  and not (s.name = @schema and t.name = @name)
            ) then 1 else 0 end;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@schema", runtimeOptions.HistoryTableSchema);
        command.Parameters.AddWithValue("@name", runtimeOptions.HistoryTableName);
        ApplyCommandTimeout(command);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;
    }

    private async ValueTask<IReadOnlyList<AppliedArtifactRecord>> ReadAppliedArtifactsAsync(
        SqlConnection connection,
        MigrationsOptions runtimeOptions,
        CancellationToken cancellationToken)
    {
        var records = new List<AppliedArtifactRecord>();
        var historyTable = FormatTable(runtimeOptions.HistoryTableSchema, runtimeOptions.HistoryTableName);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select artifact_kind, artifact_id, artifact_profile, artifact_name, checksum, applied_utc, executor
            from {historyTable}
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
        var historySchema = QuoteIdentifier(runtimeOptions.HistoryTableSchema);
        var historyTable = FormatTable(runtimeOptions.HistoryTableSchema, runtimeOptions.HistoryTableName);
        var fullNameLiteral = ToSqlUnicodeString($"{historySchema}.{QuoteIdentifier(runtimeOptions.HistoryTableName)}");

        return $"""
            if schema_id({ToSqlUnicodeString(runtimeOptions.HistoryTableSchema)}) is null
                exec(N'create schema {historySchema}');

            if object_id({fullNameLiteral}, N'U') is null
            begin
                create table {historyTable}(
                    artifact_kind nvarchar(16) not null,
                    artifact_id char(14) not null,
                    artifact_profile nvarchar(128) not null default N'',
                    artifact_name nvarchar(256) not null,
                    checksum char(64) not null,
                    applied_utc datetimeoffset(7) not null,
                    executor nvarchar(256) not null,
                    constraint {QuoteIdentifier($"PK_{runtimeOptions.HistoryTableName}")} primary key (artifact_kind, artifact_profile, artifact_id)
                );
            end;
            """;
    }

    private string RenderLockScript(MigrationsOptions runtimeOptions)
    {
        return $"""
            declare @layerzero_lock_result int;
            exec @layerzero_lock_result = sys.sp_getapplock
                @Resource = {ToSqlUnicodeString(runtimeOptions.LockName)},
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = {GetLockTimeoutMilliseconds()};

            if @layerzero_lock_result < 0
                throw 51000, 'LayerZero could not acquire the SQL Server migration lock.', 1;
            """;
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
                set xact_abort on;
                begin transaction;
                {body}
                {historyInsert}
                commit transaction;
                """;
        }

        return string.IsNullOrWhiteSpace(body)
            ? historyInsert
            : $"""
                set xact_abort on;
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
        var historyTable = FormatTable(runtimeOptions.HistoryTableSchema, runtimeOptions.HistoryTableName);
        return $"""
            insert into {historyTable}(artifact_kind, artifact_id, artifact_profile, artifact_name, checksum, applied_utc, executor)
            values (
                {ToSqlUnicodeString(artifact.Kind == MigrationArtifactKind.Migration ? "migration" : "seed")},
                {ToSqlUnicodeString(artifact.Id)},
                {ToSqlUnicodeString(artifact.HistoryProfile)},
                {ToSqlUnicodeString(artifact.Name)},
                {ToSqlUnicodeString(artifact.Checksum)},
                cast(sysutcdatetime() as datetimeoffset(7)),
                {ToSqlUnicodeString(executor)}
            );
            """;
    }

    private string RenderOperation(CompiledArtifact artifact, RelationalOperation operation, int operationIndex)
    {
        return operation switch
        {
            EnsureSchemaOperation value => $"""
                if schema_id({ToSqlUnicodeString(value.Schema)}) is null
                    exec(N'create schema {QuoteIdentifier(value.Schema)}');
                """,
            CreateTableOperation value => RenderCreateTable(value),
            DropTableOperation value => $"""
                if object_id({ToSqlUnicodeString(FormatObjectId(value.Table))}, N'U') is not null
                    drop table {FormatTable(value.Table)};
                """,
            AddColumnOperation value => $"""
                if col_length({ToSqlUnicodeString(FormatObjectId(value.Table))}, {ToSqlUnicodeString(value.Column.Name)}) is null
                    alter table {FormatTable(value.Table)}
                    add {RenderColumn(value.Column)};
                """,
            CreateIndexOperation value => $"""
                if not exists (
                    select 1
                    from sys.indexes
                    where object_id = object_id({ToSqlUnicodeString(FormatObjectId(value.Table))}, N'U')
                      and name = {ToSqlUnicodeString(value.Name)}
                )
                    create {(value.IsUnique ? "unique " : string.Empty)}index {QuoteIdentifier(value.Name)} on {FormatTable(value.Table)}({string.Join(", ", value.Columns.Select(QuoteIdentifier))});
                """,
            DropIndexOperation value => $"""
                if exists (
                    select 1
                    from sys.indexes
                    where object_id = object_id({ToSqlUnicodeString(FormatObjectId(value.Table))}, N'U')
                      and name = {ToSqlUnicodeString(value.Name)}
                )
                    drop index {QuoteIdentifier(value.Name)} on {FormatTable(value.Table)};
                """,
            InsertDataOperation value => string.Join(Environment.NewLine, value.Rows.Select(row => RenderInsert(value.Table, row))),
            UpdateDataOperation value => RenderUpdate(value.Table, value.Key, value.Values),
            DeleteDataOperation value => RenderDelete(value.Table, value.Key),
            UpsertDataOperation value => RenderUpsert(value.Table, value.KeyColumns, value.Values),
            SyncDataOperation value => RenderSync(artifact, value, operationIndex),
            SqlOperation value => value.Sql.EndsWith(';') ? value.Sql : value.Sql + ";",
            _ => throw new InvalidOperationException($"Unsupported SQL Server migration operation '{operation.GetType().FullName}'."),
        };
    }

    private string RenderCreateTable(CreateTableOperation operation)
    {
        var columnDefinitions = operation.Columns.Select(RenderColumn).ToList();
        columnDefinitions.Add(
            $"primary key ({string.Join(", ", operation.PrimaryKeyColumns.Select(QuoteIdentifier))})");

        return $"""
            if object_id({ToSqlUnicodeString(FormatObjectId(operation.Table))}, N'U') is null
            begin
                create table {FormatTable(operation.Table)}(
                    {string.Join("," + Environment.NewLine + "    ", columnDefinitions)}
                );
            end;
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
        var nonKeyColumns = values.Values.Keys
            .Where(column => !keyColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var predicate = string.Join(
            " and ",
            keyColumns.Select(column => $"{QuoteIdentifier(column)} = {ToSqlLiteral(values.Values[column])}"));

        if (nonKeyColumns.Length == 0)
        {
            return $"""
                if not exists (
                    select 1
                    from {FormatTable(table)}
                    where {predicate}
                )
                begin
                    insert into {FormatTable(table)}({string.Join(", ", values.Values.Keys.Select(QuoteIdentifier))})
                    values ({string.Join(", ", values.Values.Values.Select(ToSqlLiteral))});
                end;
                """;
        }

        return $"""
            update {FormatTable(table)}
            set {string.Join(", ", nonKeyColumns.Select(column => $"{QuoteIdentifier(column)} = {ToSqlLiteral(values.Values[column])}"))}
            where {predicate};

            if @@rowcount = 0
            begin
                insert into {FormatTable(table)}({string.Join(", ", values.Values.Keys.Select(QuoteIdentifier))})
                values ({string.Join(", ", values.Values.Values.Select(ToSqlLiteral))});
            end;
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
        var tempTableName = $"#lz_sync_{artifact.Id}_{operationIndex}";
        var tempTableColumns = columns
            .Select(column => $"{QuoteIdentifier(column)} {InferSqlType(operation.Rows, column)} null")
            .ToArray();
        var inserts = operation.Rows.Select(row => $"""
            insert into {tempTableName}({string.Join(", ", columns.Select(QuoteIdentifier))})
            values ({string.Join(", ", columns.Select(column => row.Values.TryGetValue(column, out var value) ? ToSqlLiteral(value) : "null"))});
            """);

        var keyPredicate = string.Join(
            " and ",
            operation.KeyColumns.Select(column => $"target.{QuoteIdentifier(column)} = source.{QuoteIdentifier(column)}"));
        var insertPredicate = string.Join(
            " and ",
            operation.KeyColumns.Select(column => $"existing.{QuoteIdentifier(column)} = source.{QuoteIdentifier(column)}"));
        var deletePredicate = string.Join(
            " and ",
            operation.KeyColumns.Select(column => $"source.{QuoteIdentifier(column)} = target.{QuoteIdentifier(column)}"));

        var updateStatement = nonKeyColumns.Length == 0
            ? string.Empty
            : $"""
                update target
                set {string.Join(", ", nonKeyColumns.Select(column => $"target.{QuoteIdentifier(column)} = source.{QuoteIdentifier(column)}"))}
                from {FormatTable(operation.Table)} as target
                inner join {tempTableName} as source on {keyPredicate};
                """;

        return $"""
            create table {tempTableName}(
                {string.Join("," + Environment.NewLine + "    ", tempTableColumns)}
            );

            {string.Join(Environment.NewLine, inserts)}

            {updateStatement}

            insert into {FormatTable(operation.Table)}({string.Join(", ", columns.Select(QuoteIdentifier))})
            select {string.Join(", ", columns.Select(column => $"source.{QuoteIdentifier(column)}"))}
            from {tempTableName} as source
            where not exists (
                select 1
                from {FormatTable(operation.Table)} as existing
                where {insertPredicate}
            );

            delete target
            from {FormatTable(operation.Table)} as target
            where not exists (
                select 1
                from {tempTableName} as source
                where {deletePredicate}
            );

            drop table {tempTableName};
            """;
    }

    private string RenderPredicate(ColumnValueSet key)
    {
        return string.Join(
            " and ",
            key.Values.Select(pair => $"{QuoteIdentifier(pair.Key)} = {ToSqlLiteral(pair.Value)}"));
    }

    private string RenderColumn(ColumnDefinition column)
    {
        var builder = new StringBuilder();
        builder.Append(QuoteIdentifier(column.Name))
            .Append(' ')
            .Append(ToSqlType(column.Type));

        if (column.IsIdentity)
        {
            builder.Append(" identity(1,1)");
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
            RelationalTypeKind.Int32 => "int",
            RelationalTypeKind.Int64 => "bigint",
            RelationalTypeKind.Decimal => $"decimal({type.Precision ?? 18}, {type.Scale ?? 2})",
            RelationalTypeKind.String => type.Unicode
                ? type.Length is null ? "nvarchar(max)" : $"nvarchar({type.Length.Value})"
                : type.Length is null ? "varchar(max)" : $"varchar({type.Length.Value})",
            RelationalTypeKind.Boolean => "bit",
            RelationalTypeKind.Guid => "uniqueidentifier",
            RelationalTypeKind.DateTime => "datetime2(7)",
            RelationalTypeKind.DateTimeOffset => "datetimeoffset(7)",
            RelationalTypeKind.Binary => type.Length is null ? "varbinary(max)" : $"varbinary({type.Length.Value})",
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
            null => "nvarchar(max)",
            int => "int",
            long => "bigint",
            short => "smallint",
            byte => "tinyint",
            decimal => "decimal(38, 18)",
            double => "float",
            float => "real",
            bool => "bit",
            Guid => "uniqueidentifier",
            DateTime => "datetime2(7)",
            DateTimeOffset => "datetimeoffset(7)",
            byte[] bytes => bytes.Length == 0 ? "varbinary(1)" : $"varbinary({Math.Max(bytes.Length, 1)})",
            string text => text.Length == 0 ? "nvarchar(1)" : text.Length > 4000 ? "nvarchar(max)" : $"nvarchar({text.Length})",
            _ => "nvarchar(max)",
        };
    }

    private string FormatTable(QualifiedTableName table) => FormatTable(ResolveSchema(table.Schema), table.Name);

    private string FormatTable(string schema, string tableName) => $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";

    private string FormatObjectId(QualifiedTableName table) => $"{QuoteIdentifier(ResolveSchema(table.Schema))}.{QuoteIdentifier(table.Name)}";

    private string ResolveSchema(string? schema) => string.IsNullOrWhiteSpace(schema) ? dataOptions.DefaultSchema : schema;

    private static string QuoteIdentifier(string identifier)
    {
        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static string ToSqlLiteral(object? value)
    {
        return value switch
        {
            null => "null",
            string text => ToSqlUnicodeString(text),
            bool flag => flag ? "1" : "0",
            byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture),
            Guid guidValue => ToSqlUnicodeString(guidValue.ToString("D", CultureInfo.InvariantCulture)),
            DateTime dateTimeValue => $"cast({ToSqlUnicodeString(dateTimeValue.ToString("O", CultureInfo.InvariantCulture))} as datetime2(7))",
            DateTimeOffset dateTimeOffsetValue => $"cast({ToSqlUnicodeString(dateTimeOffsetValue.ToString("O", CultureInfo.InvariantCulture))} as datetimeoffset(7))",
            byte[] bytes => $"0x{Convert.ToHexString(bytes)}",
            _ when value is IFormattable formattable => ToSqlUnicodeString(formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty),
            _ => ToSqlUnicodeString(value.ToString() ?? string.Empty),
        };
    }

    private static string ToSqlUnicodeString(string value) => $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static MigrationArtifactKind ParseArtifactKind(string value)
    {
        return value switch
        {
            "migration" => MigrationArtifactKind.Migration,
            "seed" => MigrationArtifactKind.Seed,
            _ => throw new InvalidOperationException($"Unsupported history artifact kind '{value}'."),
        };
    }

    private int GetLockTimeoutMilliseconds()
    {
        var milliseconds = options.LockTimeout.TotalMilliseconds;
        return milliseconds >= int.MaxValue ? int.MaxValue : (int)milliseconds;
    }

    private void ApplyCommandTimeout(SqlCommand command)
    {
        if (options.CommandTimeoutSeconds is { } timeout)
        {
            command.CommandTimeout = timeout;
        }
    }

    private async ValueTask ExecuteNonQueryAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
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
}

internal sealed class SqlServerMigrationLock(
    SqlConnection connection,
    string resource,
    int? commandTimeoutSeconds) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (connection.State == ConnectionState.Open)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    declare @result int;
                    exec @result = sys.sp_releaseapplock
                        @Resource = @resource,
                        @LockOwner = 'Session';
                    select @result;
                    """;
                command.Parameters.AddWithValue("@resource", resource);

                if (commandTimeoutSeconds is { } timeout)
                {
                    command.CommandTimeout = timeout;
                }

                await command.ExecuteScalarAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            SqlConnection.ClearPool(connection);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
