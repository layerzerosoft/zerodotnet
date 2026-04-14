namespace LayerZero.Data.Internal;

internal interface IEntityColumnBuilder<TEntity>
{
    string PropertyName { get; }

    bool IsKey { get; }

    EntityColumn<TEntity> Build();
}
