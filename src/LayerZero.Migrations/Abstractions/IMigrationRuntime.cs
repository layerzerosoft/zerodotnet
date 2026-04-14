namespace LayerZero.Migrations;

/// <summary>
/// Plans, validates, scripts, baselines, and applies database migrations.
/// </summary>
public interface IMigrationRuntime
{
    /// <summary>
    /// Gets migration status for the configured database.
    /// </summary>
    /// <param name="options">Optional selection options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current migration status.</returns>
    ValueTask<MigrationInfoResult> InfoAsync(
        MigrationInfoOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates local migration definitions against the configured database history.
    /// </summary>
    /// <param name="options">Optional selection options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validation result.</returns>
    ValueTask<MigrationValidationResult> ValidateAsync(
        MigrationValidationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a SQL script for the requested migration action.
    /// </summary>
    /// <param name="options">The script options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated script.</returns>
    ValueTask<MigrationScriptResult> ScriptAsync(
        MigrationScriptOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies pending migrations and selected seeds.
    /// </summary>
    /// <param name="options">Optional apply options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The apply result.</returns>
    ValueTask<MigrationApplyResult> ApplyAsync(
        MigrationApplyOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks selected artifacts as applied without executing them.
    /// </summary>
    /// <param name="options">Optional baseline options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The baseline result.</returns>
    ValueTask<MigrationBaselineResult> BaselineAsync(
        MigrationBaselineOptions? options = null,
        CancellationToken cancellationToken = default);
}
