namespace LayerZero.Migrations;

/// <summary>
/// Defines one forward-only database migration.
/// </summary>
public abstract class Migration
{
    /// <summary>
    /// Gets the per-migration transaction mode.
    /// </summary>
    public virtual MigrationTransactionMode TransactionMode => MigrationTransactionMode.Transactional;

    /// <summary>
    /// Builds the migration operations.
    /// </summary>
    /// <param name="builder">The migration builder.</param>
    public abstract void Build(MigrationBuilder builder);
}
