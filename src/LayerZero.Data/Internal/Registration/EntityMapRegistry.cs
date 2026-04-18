using LayerZero.Data.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.Internal.Registration;

internal interface IEntityMapRegistry
{
    EntityTable<TEntity> GetTable<TEntity>()
        where TEntity : notnull;

    IEntityTable GetTable(Type entityType);

    bool TryGetTable(Type entityType, out IEntityTable table);
}

internal sealed class EntityMapRegistry(
    IEnumerable<IEntityMap> maps,
    IOptions<DataOptions> optionsAccessor) : IEntityMapRegistry
{
    private readonly Dictionary<Type, IEntityTable> tableByEntityType = BuildIndex(
        maps,
        optionsAccessor.Value.Conventions.Clone());

    public EntityTable<TEntity> GetTable<TEntity>()
        where TEntity : notnull =>
        (EntityTable<TEntity>)GetTable(typeof(TEntity));

    public IEntityTable GetTable(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (!tableByEntityType.TryGetValue(entityType, out var table))
        {
            throw new InvalidOperationException($"No LayerZero data map was registered for entity type '{entityType.FullName}'.");
        }

        return table;
    }

    public bool TryGetTable(Type entityType, out IEntityTable table)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return tableByEntityType.TryGetValue(entityType, out table!);
    }

    private static Dictionary<Type, IEntityTable> BuildIndex(
        IEnumerable<IEntityMap> maps,
        DataConventionsOptions conventions)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(conventions);

        var index = new Dictionary<Type, IEntityTable>();
        foreach (var map in maps)
        {
            var table = map is IConventionEntityMap conventionAware
                ? conventionAware.GetTable(conventions)
                : map.Table;

            if (!index.TryAdd(map.EntityType, table))
            {
                throw new InvalidOperationException($"Multiple LayerZero data maps were registered for entity type '{map.EntityType.FullName}'.");
            }
        }

        return index;
    }
}
