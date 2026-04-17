namespace LayerZero.Data;

/// <summary>
/// Dispatches reusable LayerZero data queries and mutations.
/// </summary>
public interface IDataDispatcher
{
    /// <summary>
    /// Executes one reusable query.
    /// </summary>
    /// <typeparam name="TResult">The query result type.</typeparam>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The query result.</returns>
    ValueTask<TResult> QueryAsync<TResult>(IDataQuery<TResult> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one reusable mutation.
    /// </summary>
    /// <typeparam name="TResult">The mutation result type.</typeparam>
    /// <param name="mutation">The mutation to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mutation result.</returns>
    ValueTask<TResult> MutateAsync<TResult>(IDataMutation<TResult> mutation, CancellationToken cancellationToken = default);
}
