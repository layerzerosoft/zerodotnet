using System.Linq.Expressions;
using LayerZero.Data.Internal.Execution;

namespace LayerZero.Data;

/// <summary>
/// Builds one bounded LayerZero data query.
/// </summary>
/// <typeparam name="TRow">The current row type.</typeparam>
public sealed class DataQuery<TRow>
{
    private readonly IDataContextSession session;
    private readonly DataQueryModel model;

    internal DataQuery(IDataContextSession session, DataQueryModel model)
    {
        this.session = session;
        this.model = model;
    }

    /// <summary>
    /// Adds one filter predicate.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The updated query.</returns>
    public DataQuery<TRow> Where(Expression<Func<TRow, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new DataQuery<TRow>(session, model.AddFilter(predicate));
    }

    /// <summary>
    /// Adds one inner join.
    /// </summary>
    /// <typeparam name="TRight">The right entity type.</typeparam>
    /// <typeparam name="TKey">The join key type.</typeparam>
    /// <param name="leftKey">The left key selector.</param>
    /// <param name="rightKey">The right key selector.</param>
    /// <returns>The joined query.</returns>
    public DataQuery<DataJoin<TRow, TRight>> Join<TRight, TKey>(
        Expression<Func<TRow, TKey>> leftKey,
        Expression<Func<TRight, TKey>> rightKey)
        where TRight : notnull
    {
        ArgumentNullException.ThrowIfNull(leftKey);
        ArgumentNullException.ThrowIfNull(rightKey);
        return new DataQuery<DataJoin<TRow, TRight>>(session, model.AddJoin<TRight>(leftKey, rightKey));
    }

    /// <summary>
    /// Applies one projection.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <returns>The projected query.</returns>
    public DataProjectionQuery<TRow, TProjection> Select<TProjection>(Expression<Func<TRow, TProjection>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return new DataProjectionQuery<TRow, TProjection>(session, model, projection);
    }

    /// <summary>
    /// Adds one ascending ordering.
    /// </summary>
    /// <typeparam name="TValue">The ordering value type.</typeparam>
    /// <param name="selector">The ordering selector.</param>
    /// <returns>The updated query.</returns>
    public DataQuery<TRow> OrderBy<TValue>(Expression<Func<TRow, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new DataQuery<TRow>(session, model.AddOrdering(selector, descending: false));
    }

    /// <summary>
    /// Adds one descending ordering.
    /// </summary>
    /// <typeparam name="TValue">The ordering value type.</typeparam>
    /// <param name="selector">The ordering selector.</param>
    /// <returns>The updated query.</returns>
    public DataQuery<TRow> OrderByDescending<TValue>(Expression<Func<TRow, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new DataQuery<TRow>(session, model.AddOrdering(selector, descending: true));
    }

    /// <summary>
    /// Adds one secondary ascending ordering.
    /// </summary>
    /// <typeparam name="TValue">The ordering value type.</typeparam>
    /// <param name="selector">The ordering selector.</param>
    /// <returns>The updated query.</returns>
    public DataQuery<TRow> ThenBy<TValue>(Expression<Func<TRow, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new DataQuery<TRow>(session, model.AddOrdering(selector, descending: false));
    }

    /// <summary>
    /// Adds one secondary descending ordering.
    /// </summary>
    /// <typeparam name="TValue">The ordering value type.</typeparam>
    /// <param name="selector">The ordering selector.</param>
    /// <returns>The updated query.</returns>
    public DataQuery<TRow> ThenByDescending<TValue>(Expression<Func<TRow, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new DataQuery<TRow>(session, model.AddOrdering(selector, descending: true));
    }

    /// <summary>
    /// Skips one number of rows.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    /// <returns>The updated query.</returns>
    public DataQuery<TRow> Skip(int count) => new(session, model.WithSkip(count));

    /// <summary>
    /// Limits the row count.
    /// </summary>
    /// <param name="count">The maximum row count.</param>
    /// <returns>The updated query.</returns>
    public DataQuery<TRow> Take(int count) => new(session, model.WithTake(count));

    /// <summary>
    /// Materializes all rows.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The materialized rows.</returns>
    public ValueTask<IReadOnlyList<TRow>> ListAsync(CancellationToken cancellationToken = default) =>
        session.ListAsync<TRow>(model, projection: null, cancellationToken);

    /// <summary>
    /// Materializes the first row.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first row.</returns>
    public ValueTask<TRow> FirstAsync(CancellationToken cancellationToken = default) =>
        session.FirstAsync<TRow>(model, projection: null, cancellationToken);

    /// <summary>
    /// Materializes the first row when present.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first row or <see langword="default"/>.</returns>
    public ValueTask<TRow?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) =>
        session.FirstOrDefaultAsync<TRow>(model, projection: null, cancellationToken);

    /// <summary>
    /// Materializes the single row.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The single row.</returns>
    public ValueTask<TRow> SingleAsync(CancellationToken cancellationToken = default) =>
        session.SingleAsync<TRow>(model, projection: null, cancellationToken);

    /// <summary>
    /// Materializes the single row when present.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The single row or <see langword="default"/>.</returns>
    public ValueTask<TRow?> SingleOrDefaultAsync(CancellationToken cancellationToken = default) =>
        session.SingleOrDefaultAsync<TRow>(model, projection: null, cancellationToken);

    /// <summary>
    /// Determines whether any rows match.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when any rows match; otherwise <see langword="false"/>.</returns>
    public ValueTask<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        session.AggregateAsync<TRow, bool>(model, DataAggregateKind.Any, selector: null, cancellationToken);

    /// <summary>
    /// Counts the rows.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row count.</returns>
    public ValueTask<int> CountAsync(CancellationToken cancellationToken = default) =>
        session.AggregateAsync<TRow, int>(model, DataAggregateKind.Count, selector: null, cancellationToken);

    /// <summary>
    /// Counts the rows using a 64-bit result.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The row count.</returns>
    public ValueTask<long> LongCountAsync(CancellationToken cancellationToken = default) =>
        session.AggregateAsync<TRow, long>(model, DataAggregateKind.LongCount, selector: null, cancellationToken);

    /// <summary>
    /// Sums one selector.
    /// </summary>
    /// <typeparam name="TValue">The numeric value type.</typeparam>
    /// <param name="selector">The selector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sum.</returns>
    public ValueTask<TValue> SumAsync<TValue>(Expression<Func<TRow, TValue>> selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return session.AggregateAsync<TRow, TValue>(model, DataAggregateKind.Sum, selector, cancellationToken);
    }

    /// <summary>
    /// Computes the minimum value for one selector.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="selector">The selector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The minimum value.</returns>
    public ValueTask<TValue> MinAsync<TValue>(Expression<Func<TRow, TValue>> selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return session.AggregateAsync<TRow, TValue>(model, DataAggregateKind.Min, selector, cancellationToken);
    }

    /// <summary>
    /// Computes the maximum value for one selector.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="selector">The selector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The maximum value.</returns>
    public ValueTask<TValue> MaxAsync<TValue>(Expression<Func<TRow, TValue>> selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return session.AggregateAsync<TRow, TValue>(model, DataAggregateKind.Max, selector, cancellationToken);
    }
}
