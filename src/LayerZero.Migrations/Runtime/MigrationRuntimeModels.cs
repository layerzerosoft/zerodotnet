using LayerZero.Core;

namespace LayerZero.Migrations;

/// <summary>
/// Selects seed profiles for informational queries.
/// </summary>
public sealed class MigrationInfoOptions
{
    /// <summary>
    /// Gets or sets the additional seed profiles to include.
    /// </summary>
    public IReadOnlyList<string> Profiles { get; set; } = [];
}

/// <summary>
/// Selects seed profiles for validation queries.
/// </summary>
public sealed class MigrationValidationOptions
{
    /// <summary>
    /// Gets or sets the additional seed profiles to include.
    /// </summary>
    public IReadOnlyList<string> Profiles { get; set; } = [];
}

/// <summary>
/// Selects seed profiles for apply execution.
/// </summary>
public sealed class MigrationApplyOptions
{
    /// <summary>
    /// Gets or sets the additional seed profiles to include.
    /// </summary>
    public IReadOnlyList<string> Profiles { get; set; } = [];
}

/// <summary>
/// Selects artifacts for baseline execution.
/// </summary>
public sealed class MigrationBaselineOptions
{
    /// <summary>
    /// Gets or sets the additional seed profiles to baseline.
    /// </summary>
    public IReadOnlyList<string> Profiles { get; set; } = [];

    /// <summary>
    /// Gets or sets whether baseline should include the baseline seed profile.
    /// </summary>
    public bool IncludeBaselineSeedProfile { get; set; }
}

/// <summary>
/// Selects artifacts for script generation.
/// </summary>
public sealed class MigrationScriptOptions
{
    /// <summary>
    /// Gets or sets the script kind.
    /// </summary>
    public MigrationScriptKind Kind { get; set; } = MigrationScriptKind.Apply;

    /// <summary>
    /// Gets or sets the additional seed profiles to include.
    /// </summary>
    public IReadOnlyList<string> Profiles { get; set; } = [];

    /// <summary>
    /// Gets or sets whether baseline scripts should include the baseline seed profile.
    /// </summary>
    public bool IncludeBaselineSeedProfile { get; set; }
}

/// <summary>
/// Identifies a generated script kind.
/// </summary>
public enum MigrationScriptKind
{
    /// <summary>
    /// Generates a script for pending migrations and selected seeds.
    /// </summary>
    Apply = 0,

    /// <summary>
    /// Generates a script that baselines selected artifacts.
    /// </summary>
    Baseline = 1,
}

/// <summary>
/// Describes one local or applied migration artifact.
/// </summary>
public sealed class MigrationStatusItem
{
    /// <summary>
    /// Initializes a new <see cref="MigrationStatusItem"/>.
    /// </summary>
    /// <param name="kind">The artifact kind.</param>
    /// <param name="id">The artifact id.</param>
    /// <param name="name">The artifact name.</param>
    /// <param name="profile">The optional seed profile.</param>
    /// <param name="checksum">The artifact checksum.</param>
    /// <param name="transactionMode">The transaction mode.</param>
    /// <param name="isApplied">Whether the artifact is applied.</param>
    /// <param name="appliedUtc">The applied timestamp when present.</param>
    /// <param name="executor">The executor name when present.</param>
    public MigrationStatusItem(
        MigrationArtifactKind kind,
        string id,
        string name,
        string? profile,
        string checksum,
        MigrationTransactionMode transactionMode,
        bool isApplied,
        DateTimeOffset? appliedUtc,
        string? executor)
    {
        Kind = kind;
        Id = id;
        Name = name;
        Profile = profile;
        Checksum = checksum;
        TransactionMode = transactionMode;
        IsApplied = isApplied;
        AppliedUtc = appliedUtc;
        Executor = executor;
    }

    /// <summary>
    /// Gets the artifact kind.
    /// </summary>
    public MigrationArtifactKind Kind { get; }

    /// <summary>
    /// Gets the sortable UTC timestamp id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the artifact name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the seed profile when the artifact is a seed.
    /// </summary>
    public string? Profile { get; }

    /// <summary>
    /// Gets the stable checksum.
    /// </summary>
    public string Checksum { get; }

    /// <summary>
    /// Gets the transaction mode.
    /// </summary>
    public MigrationTransactionMode TransactionMode { get; }

    /// <summary>
    /// Gets whether the artifact is already applied.
    /// </summary>
    public bool IsApplied { get; }

    /// <summary>
    /// Gets the applied timestamp when present.
    /// </summary>
    public DateTimeOffset? AppliedUtc { get; }

    /// <summary>
    /// Gets the executor name when present.
    /// </summary>
    public string? Executor { get; }
}

/// <summary>
/// Reports migration status for one database.
/// </summary>
public sealed class MigrationInfoResult
{
    /// <summary>
    /// Initializes a new <see cref="MigrationInfoResult"/>.
    /// </summary>
    /// <param name="selectedProfiles">The selected seed profiles.</param>
    /// <param name="historyExists">Whether the LayerZero history table exists.</param>
    /// <param name="hasUserObjects">Whether the database already contains user objects.</param>
    /// <param name="items">The discovered artifact status items.</param>
    public MigrationInfoResult(
        IReadOnlyList<string> selectedProfiles,
        bool historyExists,
        bool hasUserObjects,
        IReadOnlyList<MigrationStatusItem> items)
    {
        SelectedProfiles = selectedProfiles;
        HistoryExists = historyExists;
        HasUserObjects = hasUserObjects;
        Items = items;
    }

    /// <summary>
    /// Gets the selected seed profiles.
    /// </summary>
    public IReadOnlyList<string> SelectedProfiles { get; }

    /// <summary>
    /// Gets whether the LayerZero history table exists.
    /// </summary>
    public bool HistoryExists { get; }

    /// <summary>
    /// Gets whether the database already contains user objects.
    /// </summary>
    public bool HasUserObjects { get; }

    /// <summary>
    /// Gets all matching artifact status items.
    /// </summary>
    public IReadOnlyList<MigrationStatusItem> Items { get; }
}

/// <summary>
/// Reports migration validation results.
/// </summary>
public sealed class MigrationValidationResult
{
    /// <summary>
    /// Initializes a new <see cref="MigrationValidationResult"/>.
    /// </summary>
    /// <param name="selectedProfiles">The selected seed profiles.</param>
    /// <param name="errors">The validation errors.</param>
    public MigrationValidationResult(IReadOnlyList<string> selectedProfiles, IReadOnlyList<Error> errors)
    {
        SelectedProfiles = selectedProfiles;
        Errors = errors;
    }

    /// <summary>
    /// Gets the selected seed profiles.
    /// </summary>
    public IReadOnlyList<string> SelectedProfiles { get; }

    /// <summary>
    /// Gets whether the current definitions are valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<Error> Errors { get; }
}

/// <summary>
/// Represents a generated migration script.
/// </summary>
public sealed class MigrationScriptResult
{
    /// <summary>
    /// Initializes a new <see cref="MigrationScriptResult"/>.
    /// </summary>
    /// <param name="kind">The script kind.</param>
    /// <param name="selectedProfiles">The selected seed profiles.</param>
    /// <param name="items">The scripted items.</param>
    /// <param name="script">The generated SQL script.</param>
    public MigrationScriptResult(
        MigrationScriptKind kind,
        IReadOnlyList<string> selectedProfiles,
        IReadOnlyList<MigrationStatusItem> items,
        string script)
    {
        Kind = kind;
        SelectedProfiles = selectedProfiles;
        Items = items;
        Script = script;
    }

    /// <summary>
    /// Gets the script kind.
    /// </summary>
    public MigrationScriptKind Kind { get; }

    /// <summary>
    /// Gets the selected seed profiles.
    /// </summary>
    public IReadOnlyList<string> SelectedProfiles { get; }

    /// <summary>
    /// Gets the scripted items.
    /// </summary>
    public IReadOnlyList<MigrationStatusItem> Items { get; }

    /// <summary>
    /// Gets the generated SQL script.
    /// </summary>
    public string Script { get; }
}

/// <summary>
/// Reports an apply execution.
/// </summary>
public sealed class MigrationApplyResult
{
    /// <summary>
    /// Initializes a new <see cref="MigrationApplyResult"/>.
    /// </summary>
    /// <param name="selectedProfiles">The selected seed profiles.</param>
    /// <param name="items">The applied items.</param>
    public MigrationApplyResult(IReadOnlyList<string> selectedProfiles, IReadOnlyList<MigrationStatusItem> items)
    {
        SelectedProfiles = selectedProfiles;
        Items = items;
    }

    /// <summary>
    /// Gets the selected seed profiles.
    /// </summary>
    public IReadOnlyList<string> SelectedProfiles { get; }

    /// <summary>
    /// Gets the applied items.
    /// </summary>
    public IReadOnlyList<MigrationStatusItem> Items { get; }
}

/// <summary>
/// Reports a baseline execution.
/// </summary>
public sealed class MigrationBaselineResult
{
    /// <summary>
    /// Initializes a new <see cref="MigrationBaselineResult"/>.
    /// </summary>
    /// <param name="selectedProfiles">The selected seed profiles.</param>
    /// <param name="items">The baselined items.</param>
    public MigrationBaselineResult(IReadOnlyList<string> selectedProfiles, IReadOnlyList<MigrationStatusItem> items)
    {
        SelectedProfiles = selectedProfiles;
        Items = items;
    }

    /// <summary>
    /// Gets the selected seed profiles.
    /// </summary>
    public IReadOnlyList<string> SelectedProfiles { get; }

    /// <summary>
    /// Gets the baselined items.
    /// </summary>
    public IReadOnlyList<MigrationStatusItem> Items { get; }
}
