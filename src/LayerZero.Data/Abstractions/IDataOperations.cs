namespace LayerZero.Data;

/// <summary>
/// Marks one reusable LayerZero data query.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IDataQuery<TResult>;

/// <summary>
/// Handles one reusable LayerZero data query.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IDataQueryHandler<in TQuery, TResult>
    where TQuery : IDataQuery<TResult>
{
    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The query instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query result.</returns>
    ValueTask<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marks one reusable LayerZero data mutation.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IDataMutation<TResult>;

/// <summary>
/// Handles one reusable LayerZero data mutation.
/// </summary>
/// <typeparam name="TMutation">The mutation type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IDataMutationHandler<in TMutation, TResult>
    where TMutation : IDataMutation<TResult>
{
    /// <summary>
    /// Handles the mutation.
    /// </summary>
    /// <param name="mutation">The mutation instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mutation result.</returns>
    ValueTask<TResult> HandleAsync(TMutation mutation, CancellationToken cancellationToken = default);
}
