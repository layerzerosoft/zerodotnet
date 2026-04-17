namespace LayerZero.Data.Internal.Registration;

internal interface IEntityMapRegistry
{
    IEntityMap GetMap(Type entityType);

    EntityTable<TEntity> GetTable<TEntity>()
        where TEntity : notnull;

    IEntityTable GetTable(Type entityType);

    bool TryGetMap(Type entityType, out IEntityMap map);
}

internal sealed class EntityMapRegistry(IEnumerable<IEntityMap> maps) : IEntityMapRegistry
{
    private readonly Dictionary<Type, IEntityMap> mapByEntityType = BuildIndex(maps);

    public IEntityMap GetMap(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (!mapByEntityType.TryGetValue(entityType, out var map))
        {
            throw new InvalidOperationException($"No LayerZero data map was registered for entity type '{entityType.FullName}'.");
        }

        return map;
    }

    public EntityTable<TEntity> GetTable<TEntity>()
        where TEntity : notnull =>
        ((EntityMap<TEntity>)GetMap(typeof(TEntity))).Table;

    public IEntityTable GetTable(Type entityType) => GetMap(entityType).Table;

    public bool TryGetMap(Type entityType, out IEntityMap map)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return mapByEntityType.TryGetValue(entityType, out map!);
    }

    private static Dictionary<Type, IEntityMap> BuildIndex(IEnumerable<IEntityMap> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);

        var index = new Dictionary<Type, IEntityMap>();
        foreach (var map in maps)
        {
            if (!index.TryAdd(map.EntityType, map))
            {
                throw new InvalidOperationException($"Multiple LayerZero data maps were registered for entity type '{map.EntityType.FullName}'.");
            }
        }

        return index;
    }
}
