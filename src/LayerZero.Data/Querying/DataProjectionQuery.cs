using System.Linq.Expressions;
using LayerZero.Data.Internal.Execution;

namespace LayerZero.Data;

/// <summary>
/// Represents one projected LayerZero data query.
/// </summary>
/// <typeparam name="TRow">The source row type.</typeparam>
/// <typeparam name="TProjection">The projection type.</typeparam>
public sealed class DataProjectionQuery<TRow, TProjection>
{
    private readonly IDataContextSession session;
    private readonly DataQueryModel model;
    private readonly Expression<Func<TRow, TProjection>> projection;

    internal DataProjectionQuery(
        IDataContextSession session,
        DataQueryModel model,
        Expression<Func<TRow, TProjection>> projection)
    {
        this.session = session;
        this.model = model;
        this.projection = projection;
    }

    /// <summary>
    /// Materializes all rows.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The materialized rows.</returns>
    public ValueTask<IReadOnlyList<TProjection>> ListAsync(CancellationToken cancellationToken = default) =>
        session.ListAsync<TProjection>(model, projection, cancellationToken);

    /// <summary>
    /// Materializes the first row.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first row.</returns>
    public ValueTask<TProjection> FirstAsync(CancellationToken cancellationToken = default) =>
        session.FirstAsync<TProjection>(model, projection, cancellationToken);

    /// <summary>
    /// Materializes the first row when present.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first row or <see langword="default"/>.</returns>
    public ValueTask<TProjection?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) =>
        session.FirstOrDefaultAsync<TProjection>(model, projection, cancellationToken);

    /// <summary>
    /// Materializes the single row.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The single row.</returns>
    public ValueTask<TProjection> SingleAsync(CancellationToken cancellationToken = default) =>
        session.SingleAsync<TProjection>(model, projection, cancellationToken);

    /// <summary>
    /// Materializes the single row when present.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The single row or <see langword="default"/>.</returns>
    public ValueTask<TProjection?> SingleOrDefaultAsync(CancellationToken cancellationToken = default) =>
        session.SingleOrDefaultAsync<TProjection>(model, projection, cancellationToken);
}
