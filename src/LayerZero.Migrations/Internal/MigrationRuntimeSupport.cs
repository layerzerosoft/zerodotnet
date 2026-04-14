using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LayerZero.Core;
using LayerZero.Migrations.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Migrations.Internal;

internal interface IMigrationDatabaseAdapter
{
    ValueTask<MigrationDatabaseSnapshot> ReadStateAsync(MigrationsOptions options, CancellationToken cancellationToken);

    ValueTask EnsureHistoryStoreAsync(MigrationsOptions options, CancellationToken cancellationToken);

    ValueTask<IAsyncDisposable> AcquireLockAsync(MigrationsOptions options, CancellationToken cancellationToken);

    string GenerateScript(
        MigrationExecutionMode mode,
        MigrationsOptions options,
        string executor,
        IReadOnlyList<CompiledArtifact> artifacts);

    ValueTask ExecuteAsync(
        MigrationExecutionMode mode,
        MigrationsOptions options,
        string executor,
        IReadOnlyList<CompiledArtifact> artifacts,
        CancellationToken cancellationToken);
}

internal enum MigrationExecutionMode
{
    Apply = 0,
    Baseline = 1,
}

internal sealed class AppliedArtifactRecord
{
    public AppliedArtifactRecord(
        MigrationArtifactKind kind,
        string id,
        string profile,
        string name,
        string checksum,
        DateTimeOffset appliedUtc,
        string executor)
    {
        Kind = kind;
        Id = id;
        Profile = profile;
        Name = name;
        Checksum = checksum;
        AppliedUtc = appliedUtc;
        Executor = executor;
    }

    public MigrationArtifactKind Kind { get; }

    public string Id { get; }

    public string Profile { get; }

    public string Name { get; }

    public string Checksum { get; }

    public DateTimeOffset AppliedUtc { get; }

    public string Executor { get; }
}

internal sealed class MigrationDatabaseSnapshot
{
    public MigrationDatabaseSnapshot(
        bool historyExists,
        bool hasUserObjects,
        IReadOnlyList<AppliedArtifactRecord> appliedArtifacts)
    {
        HistoryExists = historyExists;
        HasUserObjects = hasUserObjects;
        AppliedArtifacts = appliedArtifacts;
    }

    public bool HistoryExists { get; }

    public bool HasUserObjects { get; }

    public IReadOnlyList<AppliedArtifactRecord> AppliedArtifacts { get; }
}

internal sealed class CompiledArtifact
{
    public CompiledArtifact(
        MigrationArtifactKind kind,
        string id,
        string name,
        string profile,
        MigrationTransactionMode transactionMode,
        IReadOnlyList<RelationalOperation> operations,
        string checksum)
    {
        Kind = kind;
        Id = id;
        Name = name;
        Profile = profile;
        TransactionMode = transactionMode;
        Operations = operations;
        Checksum = checksum;
    }

    public MigrationArtifactKind Kind { get; }

    public string Id { get; }

    public string Name { get; }

    public string Profile { get; }

    public MigrationTransactionMode TransactionMode { get; }

    public IReadOnlyList<RelationalOperation> Operations { get; }

    public string Checksum { get; }

    public string HistoryProfile => Kind == MigrationArtifactKind.Seed ? Profile : string.Empty;
}

internal sealed class CompiledMigrationModel
{
    public CompiledMigrationModel(
        IReadOnlyList<CompiledArtifact> migrations,
        IReadOnlyList<CompiledArtifact> seeds)
    {
        Migrations = migrations;
        Seeds = seeds;
    }

    public IReadOnlyList<CompiledArtifact> Migrations { get; }

    public IReadOnlyList<CompiledArtifact> Seeds { get; }
}

internal sealed class MigrationModelCompiler
{
    private static readonly Regex TimestampIdPattern = new("^[0-9]{14}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ProfilePattern = new("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public CompiledMigrationModel Compile(IMigrationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var migrations = registry.Migrations
            .Select(CompileMigration)
            .OrderBy(static artifact => artifact.Id, StringComparer.Ordinal)
            .ToArray();

        var duplicateMigrationIds = migrations
            .GroupBy(static artifact => artifact.Id, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateMigrationIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate migration ids are not supported: {string.Join(", ", duplicateMigrationIds)}.");
        }

        var seeds = registry.Seeds
            .Select(CompileSeed)
            .OrderBy(static artifact => artifact.Profile.Equals(SeedProfiles.Baseline, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(static artifact => artifact.Profile, StringComparer.Ordinal)
            .ThenBy(static artifact => artifact.Id, StringComparer.Ordinal)
            .ToArray();

        var duplicateSeeds = seeds
            .GroupBy(static artifact => $"{artifact.Profile}|{artifact.Id}", StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateSeeds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate seed ids within one profile are not supported: {string.Join(", ", duplicateSeeds)}.");
        }

        return new CompiledMigrationModel(migrations, seeds);
    }

    private static CompiledArtifact CompileMigration(MigrationDescriptor descriptor)
    {
        ValidateId(descriptor.Id, "migration");
        var builder = new MigrationBuilder();
        var migration = descriptor.CreateInstance();
        migration.Build(builder);
        var operations = builder.Build().ToArray();
        ValidateOperations(operations, descriptor.Id);
        return new CompiledArtifact(
            MigrationArtifactKind.Migration,
            descriptor.Id,
            descriptor.Name,
            string.Empty,
            descriptor.TransactionMode,
            operations,
            ComputeChecksum(MigrationArtifactKind.Migration, descriptor.Id, descriptor.Name, string.Empty, descriptor.TransactionMode, operations));
    }

    private static CompiledArtifact CompileSeed(SeedDescriptor descriptor)
    {
        ValidateId(descriptor.Id, "seed");
        ValidateProfile(descriptor.Profile);
        var builder = new SeedBuilder();
        var seed = descriptor.CreateInstance();
        seed.Build(builder);
        var operations = builder.Build().ToArray();
        ValidateOperations(operations, descriptor.Id);
        return new CompiledArtifact(
            MigrationArtifactKind.Seed,
            descriptor.Id,
            descriptor.Name,
            descriptor.Profile,
            MigrationTransactionMode.Transactional,
            operations,
            ComputeChecksum(MigrationArtifactKind.Seed, descriptor.Id, descriptor.Name, descriptor.Profile, MigrationTransactionMode.Transactional, operations));
    }

    private static void ValidateId(string id, string kind)
    {
        if (!TimestampIdPattern.IsMatch(id))
        {
            throw new InvalidOperationException(
                $"The {kind} id '{id}' must be a 14-digit UTC timestamp like yyyyMMddHHmmss.");
        }
    }

    private static void ValidateProfile(string profile)
    {
        if (!ProfilePattern.IsMatch(profile))
        {
            throw new InvalidOperationException(
                $"The seed profile '{profile}' must use lowercase letters, numbers, or dashes.");
        }
    }

    private static void ValidateOperations(IReadOnlyList<RelationalOperation> operations, string id)
    {
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case CreateTableOperation createTable when createTable.Columns.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' creates table '{createTable.Table.Name}' without columns.");
                case CreateTableOperation createTable when createTable.PrimaryKeyColumns.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' creates table '{createTable.Table.Name}' without a primary key.");
                case CreateIndexOperation createIndex when createIndex.Columns.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' creates index '{createIndex.Name}' without columns.");
                case InsertDataOperation insert when insert.Rows.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' inserts zero rows.");
                case InsertDataOperation insert when insert.Rows.Any(static row => row.Values.Count == 0):
                    throw new InvalidOperationException($"Migration artifact '{id}' inserts an empty row.");
                case UpdateDataOperation update when update.Key.Values.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' updates rows without a key predicate.");
                case UpdateDataOperation update when update.Values.Values.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' updates rows without any assigned values.");
                case DeleteDataOperation delete when delete.Key.Values.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' deletes rows without a key predicate.");
                case UpsertDataOperation upsert when upsert.Values.Values.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' upserts without any assigned values.");
                case SyncDataOperation sync when sync.Rows.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' syncs zero rows.");
                case UpsertDataOperation upsert when upsert.KeyColumns.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' upserts without key columns.");
                case UpsertDataOperation upsert when upsert.KeyColumns.Any(key => !upsert.Values.Values.ContainsKey(key)):
                    throw new InvalidOperationException($"Migration artifact '{id}' upserts values that do not contain every key column.");
                case SyncDataOperation sync when sync.KeyColumns.Count == 0:
                    throw new InvalidOperationException($"Migration artifact '{id}' syncs without key columns.");
                case SyncDataOperation sync when sync.Rows.Any(row => sync.KeyColumns.Any(key => !row.Values.ContainsKey(key))):
                    throw new InvalidOperationException($"Migration artifact '{id}' syncs rows that do not contain every key column.");
            }
        }
    }

    private static string ComputeChecksum(
        MigrationArtifactKind kind,
        string id,
        string name,
        string profile,
        MigrationTransactionMode transactionMode,
        IReadOnlyList<RelationalOperation> operations)
    {
        var builder = new StringBuilder();
        builder.Append(kind);
        builder.Append('|');
        builder.Append(id);
        builder.Append('|');
        builder.Append(name);
        builder.Append('|');
        builder.Append(profile);
        builder.Append('|');
        builder.Append((int)transactionMode);

        foreach (var operation in operations)
        {
            AppendOperation(builder, operation);
        }

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static void AppendOperation(StringBuilder builder, RelationalOperation operation)
    {
        switch (operation)
        {
            case EnsureSchemaOperation value:
                builder.Append("|ensure-schema:").Append(value.Schema);
                break;
            case CreateTableOperation value:
                builder.Append("|create-table:");
                AppendTable(builder, value.Table);
                foreach (var column in value.Columns.OrderBy(static column => column.Name, StringComparer.Ordinal))
                {
                    AppendColumn(builder, column);
                }

                builder.Append("|pk:");
                foreach (var keyColumn in value.PrimaryKeyColumns.OrderBy(static column => column, StringComparer.Ordinal))
                {
                    builder.Append(keyColumn).Append(',');
                }

                break;
            case DropTableOperation value:
                builder.Append("|drop-table:");
                AppendTable(builder, value.Table);
                break;
            case AddColumnOperation value:
                builder.Append("|add-column:");
                AppendTable(builder, value.Table);
                AppendColumn(builder, value.Column);
                break;
            case CreateIndexOperation value:
                builder.Append("|create-index:");
                AppendTable(builder, value.Table);
                builder.Append(value.Name).Append('|').Append(value.IsUnique ? '1' : '0');
                foreach (var column in value.Columns.OrderBy(static column => column, StringComparer.Ordinal))
                {
                    builder.Append('|').Append(column);
                }

                break;
            case DropIndexOperation value:
                builder.Append("|drop-index:");
                AppendTable(builder, value.Table);
                builder.Append(value.Name);
                break;
            case InsertDataOperation value:
                builder.Append("|insert:");
                AppendTable(builder, value.Table);
                AppendRows(builder, value.Rows);
                break;
            case UpdateDataOperation value:
                builder.Append("|update:");
                AppendTable(builder, value.Table);
                AppendRow(builder, value.Key);
                AppendRow(builder, value.Values);
                break;
            case DeleteDataOperation value:
                builder.Append("|delete:");
                AppendTable(builder, value.Table);
                AppendRow(builder, value.Key);
                break;
            case UpsertDataOperation value:
                builder.Append("|upsert:");
                AppendTable(builder, value.Table);
                foreach (var column in value.KeyColumns.OrderBy(static column => column, StringComparer.Ordinal))
                {
                    builder.Append('|').Append(column);
                }

                AppendRow(builder, value.Values);
                break;
            case SyncDataOperation value:
                builder.Append("|sync:");
                AppendTable(builder, value.Table);
                foreach (var column in value.KeyColumns.OrderBy(static column => column, StringComparer.Ordinal))
                {
                    builder.Append('|').Append(column);
                }

                AppendRows(builder, value.Rows);
                break;
            case SqlOperation value:
                builder.Append("|sql:").Append(value.Sql);
                break;
            default:
                throw new InvalidOperationException($"Unsupported migration operation type '{operation.GetType().FullName}'.");
        }
    }

    private static void AppendTable(StringBuilder builder, QualifiedTableName table)
    {
        builder.Append(table.Schema ?? string.Empty).Append('.').Append(table.Name);
    }

    private static void AppendColumn(StringBuilder builder, ColumnDefinition column)
    {
        builder.Append("|column:")
            .Append(column.Name)
            .Append(':')
            .Append(column.Type.Kind)
            .Append(':')
            .Append(column.Type.Length?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
            .Append(':')
            .Append(column.Type.Unicode ? '1' : '0')
            .Append(':')
            .Append(column.Type.Precision?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
            .Append(':')
            .Append(column.Type.Scale?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
            .Append(':')
            .Append(column.IsNullable ? '1' : '0')
            .Append(':')
            .Append(column.IsIdentity ? '1' : '0')
            .Append(':');
        AppendScalar(builder, column.DefaultValue);
    }

    private static void AppendRows(StringBuilder builder, IReadOnlyList<ColumnValueSet> rows)
    {
        foreach (var row in rows)
        {
            AppendRow(builder, row);
        }
    }

    private static void AppendRow(StringBuilder builder, ColumnValueSet row)
    {
        builder.Append("|row");
        foreach (var pair in row.Values.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append('|').Append(pair.Key).Append('=');
            AppendScalar(builder, pair.Value);
        }
    }

    private static void AppendScalar(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("<null>");
                return;
            case string stringValue:
                builder.Append("string:").Append(stringValue);
                return;
            case bool boolValue:
                builder.Append("bool:").Append(boolValue ? '1' : '0');
                return;
            case Guid guidValue:
                builder.Append("guid:").Append(guidValue.ToString("D", CultureInfo.InvariantCulture));
                return;
            case DateTime dateTimeValue:
                builder.Append("datetime:").Append(dateTimeValue.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                return;
            case DateTimeOffset dateTimeOffsetValue:
                builder.Append("datetimeoffset:").Append(dateTimeOffsetValue.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                return;
            case byte[] bytes:
                builder.Append("bytes:").Append(Convert.ToHexString(bytes));
                return;
        }

        if (value is IFormattable formattable)
        {
            builder.Append(value.GetType().FullName).Append(':').Append(formattable.ToString(null, CultureInfo.InvariantCulture));
            return;
        }

        builder.Append(value.GetType().FullName).Append(':').Append(value);
    }
}

internal sealed class MigrationRuntime(
    IMigrationRegistry registry,
    IMigrationDatabaseAdapter adapter,
    MigrationModelCompiler compiler,
    IOptions<MigrationsOptions> optionsAccessor) : IMigrationRuntime
{
    private readonly IMigrationRegistry registry = registry;
    private readonly IMigrationDatabaseAdapter adapter = adapter;
    private readonly MigrationModelCompiler compiler = compiler;
    private readonly MigrationsOptions options = optionsAccessor.Value;

    public async ValueTask<MigrationInfoResult> InfoAsync(
        MigrationInfoOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var selectedProfiles = GetSelectedProfiles(options?.Profiles, includeBaselineProfile: true);
        var model = compiler.Compile(registry);
        var state = await adapter.ReadStateAsync(this.options, cancellationToken).ConfigureAwait(false);
        var items = BuildStatusItems(model, selectedProfiles, state).ToArray();
        return new MigrationInfoResult(selectedProfiles, state.HistoryExists, state.HasUserObjects, items);
    }

    public async ValueTask<MigrationValidationResult> ValidateAsync(
        MigrationValidationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var selectedProfiles = GetSelectedProfiles(options?.Profiles, includeBaselineProfile: true);
        var model = compiler.Compile(registry);
        var state = await adapter.ReadStateAsync(this.options, cancellationToken).ConfigureAwait(false);
        var errors = Validate(model, selectedProfiles, state).ToArray();
        return new MigrationValidationResult(selectedProfiles, errors);
    }

    public async ValueTask<MigrationScriptResult> ScriptAsync(
        MigrationScriptOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MigrationScriptOptions();
        var selectedProfiles = GetSelectedProfiles(options.Profiles, options.Kind == MigrationScriptKind.Apply || options.IncludeBaselineSeedProfile);
        var model = compiler.Compile(registry);
        var state = await adapter.ReadStateAsync(this.options, cancellationToken).ConfigureAwait(false);
        EnsureNoValidationErrors(model, selectedProfiles, state);
        var artifacts = options.Kind == MigrationScriptKind.Apply
            ? GetPendingArtifacts(model, selectedProfiles, state).ToArray()
            : GetBaselineCandidates(model, selectedProfiles, state, options.IncludeBaselineSeedProfile).ToArray();
        var script = adapter.GenerateScript(
            options.Kind == MigrationScriptKind.Apply ? MigrationExecutionMode.Apply : MigrationExecutionMode.Baseline,
            this.options,
            this.options.Executor,
            artifacts);
        return new MigrationScriptResult(
            options.Kind,
            selectedProfiles,
            ToStatusItems(artifacts, isApplied: false, appliedUtc: null, executor: null).ToArray(),
            script);
    }

    public async ValueTask<MigrationApplyResult> ApplyAsync(
        MigrationApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var selectedProfiles = GetSelectedProfiles(options?.Profiles, includeBaselineProfile: true);
        var model = compiler.Compile(registry);

        await using var migrationLock = await adapter.AcquireLockAsync(this.options, cancellationToken).ConfigureAwait(false);
        var state = await adapter.ReadStateAsync(this.options, cancellationToken).ConfigureAwait(false);
        if (!state.HistoryExists && state.HasUserObjects)
        {
            throw new InvalidOperationException(
                "LayerZero refused to take over a non-empty database without migration history. Run baseline first.");
        }

        await adapter.EnsureHistoryStoreAsync(this.options, cancellationToken).ConfigureAwait(false);
        state = await adapter.ReadStateAsync(this.options, cancellationToken).ConfigureAwait(false);
        EnsureNoValidationErrors(model, selectedProfiles, state);
        var artifacts = GetPendingArtifacts(model, selectedProfiles, state).ToArray();
        await adapter.ExecuteAsync(MigrationExecutionMode.Apply, this.options, this.options.Executor, artifacts, cancellationToken).ConfigureAwait(false);
        return new MigrationApplyResult(
            selectedProfiles,
            ToStatusItems(artifacts, isApplied: true, appliedUtc: DateTimeOffset.UtcNow, executor: this.options.Executor).ToArray());
    }

    public async ValueTask<MigrationBaselineResult> BaselineAsync(
        MigrationBaselineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MigrationBaselineOptions();
        var selectedProfiles = GetSelectedProfiles(options.Profiles, options.IncludeBaselineSeedProfile);
        var model = compiler.Compile(registry);

        await using var migrationLock = await adapter.AcquireLockAsync(this.options, cancellationToken).ConfigureAwait(false);
        await adapter.EnsureHistoryStoreAsync(this.options, cancellationToken).ConfigureAwait(false);
        var state = await adapter.ReadStateAsync(this.options, cancellationToken).ConfigureAwait(false);
        EnsureNoValidationErrors(model, selectedProfiles, state);
        var artifacts = GetBaselineCandidates(model, selectedProfiles, state, options.IncludeBaselineSeedProfile).ToArray();
        await adapter.ExecuteAsync(MigrationExecutionMode.Baseline, this.options, this.options.Executor, artifacts, cancellationToken).ConfigureAwait(false);
        return new MigrationBaselineResult(
            selectedProfiles,
            ToStatusItems(artifacts, isApplied: true, appliedUtc: DateTimeOffset.UtcNow, executor: this.options.Executor).ToArray());
    }

    private static IReadOnlyList<string> GetSelectedProfiles(IEnumerable<string>? profiles, bool includeBaselineProfile)
    {
        var selected = new List<string>();
        if (includeBaselineProfile)
        {
            selected.Add(SeedProfiles.Baseline);
        }

        foreach (var profile in profiles ?? [])
        {
            if (string.IsNullOrWhiteSpace(profile))
            {
                continue;
            }

            if (!selected.Contains(profile, StringComparer.Ordinal))
            {
                selected.Add(profile);
            }
        }

        return selected;
    }

    private static IEnumerable<MigrationStatusItem> BuildStatusItems(
        CompiledMigrationModel model,
        IReadOnlyList<string> selectedProfiles,
        MigrationDatabaseSnapshot state)
    {
        var applied = state.AppliedArtifacts.ToDictionary(
            static item => (item.Kind, item.Profile, item.Id),
            static item => item);

        foreach (var artifact in model.Migrations)
        {
            applied.TryGetValue((artifact.Kind, artifact.HistoryProfile, artifact.Id), out var appliedRecord);
            yield return ToStatusItem(artifact, appliedRecord);
        }

        foreach (var artifact in model.Seeds.Where(seed => selectedProfiles.Contains(seed.Profile, StringComparer.Ordinal)))
        {
            applied.TryGetValue((artifact.Kind, artifact.HistoryProfile, artifact.Id), out var appliedRecord);
            yield return ToStatusItem(artifact, appliedRecord);
        }
    }

    private static MigrationStatusItem ToStatusItem(CompiledArtifact artifact, AppliedArtifactRecord? appliedRecord)
    {
        return new MigrationStatusItem(
            artifact.Kind,
            artifact.Id,
            artifact.Name,
            artifact.Kind == MigrationArtifactKind.Seed ? artifact.Profile : null,
            artifact.Checksum,
            artifact.TransactionMode,
            appliedRecord is not null,
            appliedRecord?.AppliedUtc,
            appliedRecord?.Executor);
    }

    private static IEnumerable<MigrationStatusItem> ToStatusItems(
        IReadOnlyList<CompiledArtifact> artifacts,
        bool isApplied,
        DateTimeOffset? appliedUtc,
        string? executor)
    {
        return artifacts.Select(artifact => new MigrationStatusItem(
            artifact.Kind,
            artifact.Id,
            artifact.Name,
            artifact.Kind == MigrationArtifactKind.Seed ? artifact.Profile : null,
            artifact.Checksum,
            artifact.TransactionMode,
            isApplied,
            appliedUtc,
            executor));
    }

    private static IReadOnlyList<Error> Validate(
        CompiledMigrationModel model,
        IReadOnlyList<string> selectedProfiles,
        MigrationDatabaseSnapshot state)
    {
        var errors = new List<Error>();
        var localArtifacts = model.Migrations
            .Concat(model.Seeds)
            .ToArray();

        var localByKey = localArtifacts.ToDictionary(
            static artifact => (artifact.Kind, artifact.HistoryProfile, artifact.Id),
            static artifact => artifact);

        foreach (var applied in state.AppliedArtifacts)
        {
            if (!localByKey.TryGetValue((applied.Kind, applied.Profile, applied.Id), out var local))
            {
                errors.Add(new Error(
                    "layerzero.migrations.missing_local_artifact",
                    $"Applied artifact '{applied.Kind}:{applied.Profile}:{applied.Id}' is missing from the local source-controlled definitions."));
                continue;
            }

            if (!string.Equals(local.Checksum, applied.Checksum, StringComparison.Ordinal))
            {
                errors.Add(new Error(
                    "layerzero.migrations.checksum_mismatch",
                    $"Artifact '{applied.Kind}:{applied.Profile}:{applied.Id}' checksum drifted since it was applied."));
            }
        }

        ValidateOutOfOrder(errors, model.Migrations, state.AppliedArtifacts.Where(static item => item.Kind == MigrationArtifactKind.Migration), string.Empty);

        foreach (var profile in selectedProfiles)
        {
            ValidateOutOfOrder(
                errors,
                model.Seeds.Where(seed => string.Equals(seed.Profile, profile, StringComparison.Ordinal)),
                state.AppliedArtifacts.Where(item =>
                    item.Kind == MigrationArtifactKind.Seed
                    && string.Equals(item.Profile, profile, StringComparison.Ordinal)),
                profile);
        }

        return errors;
    }

    private static void ValidateOutOfOrder(
        ICollection<Error> errors,
        IEnumerable<CompiledArtifact> localArtifacts,
        IEnumerable<AppliedArtifactRecord> appliedArtifacts,
        string profile)
    {
        var latestApplied = appliedArtifacts
            .Select(static item => item.Id)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .LastOrDefault();

        if (latestApplied is null)
        {
            return;
        }

        foreach (var local in localArtifacts.Where(local => StringComparer.Ordinal.Compare(local.Id, latestApplied) < 0))
        {
            if (appliedArtifacts.Any(applied => string.Equals(applied.Id, local.Id, StringComparison.Ordinal)))
            {
                continue;
            }

            var suffix = string.IsNullOrEmpty(profile) ? string.Empty : $" in profile '{profile}'";
            errors.Add(new Error(
                "layerzero.migrations.out_of_order",
                $"Artifact '{local.Id}' was introduced out of order{suffix}."));
        }
    }

    private static IEnumerable<CompiledArtifact> GetPendingArtifacts(
        CompiledMigrationModel model,
        IReadOnlyList<string> selectedProfiles,
        MigrationDatabaseSnapshot state)
    {
        var applied = state.AppliedArtifacts.ToDictionary(static item => (item.Kind, item.Profile, item.Id));

        foreach (var migration in model.Migrations)
        {
            if (!applied.ContainsKey((migration.Kind, migration.HistoryProfile, migration.Id)))
            {
                yield return migration;
            }
        }

        foreach (var seed in model.Seeds.Where(seed => selectedProfiles.Contains(seed.Profile, StringComparer.Ordinal)))
        {
            if (!applied.ContainsKey((seed.Kind, seed.HistoryProfile, seed.Id)))
            {
                yield return seed;
            }
        }
    }

    private static IEnumerable<CompiledArtifact> GetBaselineCandidates(
        CompiledMigrationModel model,
        IReadOnlyList<string> selectedProfiles,
        MigrationDatabaseSnapshot state,
        bool includeBaselineSeeds)
    {
        var applied = state.AppliedArtifacts.ToDictionary(static item => (item.Kind, item.Profile, item.Id));

        foreach (var migration in model.Migrations)
        {
            if (!applied.ContainsKey((migration.Kind, migration.HistoryProfile, migration.Id)))
            {
                yield return migration;
            }
        }

        foreach (var seed in model.Seeds.Where(seed =>
                     selectedProfiles.Contains(seed.Profile, StringComparer.Ordinal)
                     && (includeBaselineSeeds || !string.Equals(seed.Profile, SeedProfiles.Baseline, StringComparison.Ordinal))))
        {
            if (!applied.ContainsKey((seed.Kind, seed.HistoryProfile, seed.Id)))
            {
                yield return seed;
            }
        }
    }

    private static void EnsureNoValidationErrors(
        CompiledMigrationModel model,
        IReadOnlyList<string> selectedProfiles,
        MigrationDatabaseSnapshot state)
    {
        var errors = Validate(model, selectedProfiles, state);
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(static error => error.Message)));
    }
}
