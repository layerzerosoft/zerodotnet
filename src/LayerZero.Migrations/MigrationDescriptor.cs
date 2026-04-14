namespace LayerZero.Migrations;

/// <summary>
/// Describes one discovered migration artifact.
/// </summary>
public sealed class MigrationDescriptor
{
    private readonly Func<Migration> factory;

    /// <summary>
    /// Initializes a new <see cref="MigrationDescriptor"/>.
    /// </summary>
    /// <param name="id">The sortable UTC timestamp id.</param>
    /// <param name="name">The human-readable migration name.</param>
    /// <param name="migrationType">The migration CLR type.</param>
    /// <param name="factory">The migration factory.</param>
    public MigrationDescriptor(
        string id,
        string name,
        Type migrationType,
        Func<Migration> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(migrationType);
        ArgumentNullException.ThrowIfNull(factory);

        Id = id;
        Name = name;
        MigrationType = migrationType;
        this.factory = factory;
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
    /// Gets the migration CLR type.
    /// </summary>
    public Type MigrationType { get; }

    /// <summary>
    /// Creates a migration instance.
    /// </summary>
    /// <returns>The created migration.</returns>
    public Migration CreateInstance() => factory();
}
