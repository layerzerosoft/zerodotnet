namespace LayerZero.Migrations;

/// <summary>
/// Controls how a migration is wrapped in transactions.
/// </summary>
public enum MigrationTransactionMode
{
    /// <summary>
    /// Executes the migration in a database transaction.
    /// </summary>
    Transactional = 0,

    /// <summary>
    /// Executes the migration without a surrounding database transaction.
    /// </summary>
    NonTransactional = 1,
}
