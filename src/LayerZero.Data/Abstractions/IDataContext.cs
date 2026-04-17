namespace LayerZero.Data;

/// <summary>
/// Provides the main LayerZero data access surface.
/// </summary>
public interface IDataContext
{
    /// <summary>
    /// Starts a typed query rooted at one mapped entity.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <returns>The typed query builder.</returns>
    DataQuery<TEntity> Query<TEntity>()
        where TEntity : notnull;

    /// <summary>
    /// Inserts one entity row.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed operation when the insert succeeds.</returns>
    ValueTask InsertAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : notnull;

    /// <summary>
    /// Starts an explicit update operation.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <returns>The typed update builder.</returns>
    DataUpdate<TEntity> Update<TEntity>()
        where TEntity : notnull;

    /// <summary>
    /// Starts an explicit delete operation.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <returns>The typed delete builder.</returns>
    DataDelete<TEntity> Delete<TEntity>()
        where TEntity : notnull;

    /// <summary>
    /// Begins an explicit data scope that reuses one connection and transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The active scope.</returns>
    ValueTask<IDataScope> BeginScopeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw SQL escape hatch.
    /// </summary>
    /// <returns>The raw SQL context.</returns>
    IDataSqlContext Sql();
}
