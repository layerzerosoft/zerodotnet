using System.Linq.Expressions;
using LayerZero.Data.Internal.Execution;

namespace LayerZero.Data;

/// <summary>
/// Builds one explicit delete mutation.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type.</typeparam>
public sealed class DataDelete<TEntity>
    where TEntity : notnull
{
    private readonly IDataContextSession session;
    private readonly DataDeleteModel model;

    internal DataDelete(IDataContextSession session, DataDeleteModel model)
    {
        this.session = session;
        this.model = model;
    }

    /// <summary>
    /// Adds one filter predicate.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The updated builder.</returns>
    public DataDelete<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new DataDelete<TEntity>(session, model.AddFilter(predicate));
    }

    /// <summary>
    /// Executes the delete.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The affected row count.</returns>
    public ValueTask<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
        session.ExecuteDeleteAsync<TEntity>(model, cancellationToken);
}
