namespace LayerZero.Migrations;

/// <summary>
/// Defines one forward-only database migration.
/// </summary>
public abstract class Migration
{
    /// <summary>
    /// Initializes a new <see cref="Migration"/>.
    /// </summary>
    /// <param name="id">The sortable UTC timestamp id.</param>
    /// <param name="name">The human-readable migration name.</param>
    /// <param name="transactionMode">The per-migration transaction mode.</param>
    protected Migration(
        string id,
        string name,
        MigrationTransactionMode transactionMode = MigrationTransactionMode.Transactional)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        TransactionMode = transactionMode;
    }

    /// <summary>
    /// Gets the sortable UTC timestamp id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the human-readable migration name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the per-migration transaction mode.
    /// </summary>
    public MigrationTransactionMode TransactionMode { get; }

    /// <summary>
    /// Builds the migration operations.
    /// </summary>
    /// <param name="builder">The migration builder.</param>
    public abstract void Build(MigrationBuilder builder);
}
