using System.Linq.Expressions;
using LayerZero.Data.Internal.Execution;

namespace LayerZero.Data;

/// <summary>
/// Builds one explicit update mutation.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
public sealed class DataUpdate<TEntity>
    where TEntity : notnull
{
    private readonly IDataContextSession session;
    private readonly DataUpdateModel model;

    internal DataUpdate(IDataContextSession session, DataUpdateModel model)
    {
        this.session = session;
        this.model = model;
    }

    /// <summary>
    /// Adds one filter predicate.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The updated builder.</returns>
    public DataUpdate<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new DataUpdate<TEntity>(session, model.AddFilter(predicate));
    }

    /// <summary>
    /// Adds one assignment.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="property">The property selector.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns>The updated builder.</returns>
    public DataUpdate<TEntity> Set<TProperty>(Expression<Func<TEntity, TProperty>> property, TProperty value)
    {
        ArgumentNullException.ThrowIfNull(property);
        return new DataUpdate<TEntity>(session, model.AddAssignment(property, value, typeof(TProperty)));
    }

    /// <summary>
    /// Executes the update.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The affected row count.</returns>
    public ValueTask<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
        session.ExecuteUpdateAsync<TEntity>(model, cancellationToken);
}
