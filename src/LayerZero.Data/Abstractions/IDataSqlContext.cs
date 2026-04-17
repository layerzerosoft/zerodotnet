namespace LayerZero.Data;

/// <summary>
/// Provides raw SQL execution with parameterized interpolated strings.
/// </summary>
public interface IDataSqlContext
{
    /// <summary>
    /// Executes one SQL command.
    /// </summary>
    /// <param name="statement">The SQL statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The affected row count.</returns>
    ValueTask<int> ExecuteAsync(DataSqlStatement statement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one SQL command built from an interpolated string.
    /// </summary>
    /// <param name="statement">The SQL statement handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The affected row count.</returns>
    ValueTask<int> ExecuteAsync(DataSqlInterpolatedStringHandler statement, CancellationToken cancellationToken = default) =>
        ExecuteAsync(statement.Build(), cancellationToken);

    /// <summary>
    /// Executes one SQL query and materializes all rows.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The materialized rows.</returns>
    ValueTask<IReadOnlyList<TResult>> ListAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one SQL query built from an interpolated string and materializes all rows.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The materialized rows.</returns>
    ValueTask<IReadOnlyList<TResult>> ListAsync<TResult>(DataSqlInterpolatedStringHandler statement, CancellationToken cancellationToken = default) =>
        ListAsync<TResult>(statement.Build(), cancellationToken);

    /// <summary>
    /// Executes one SQL query and materializes the first row.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first row.</returns>
    ValueTask<TResult> FirstAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one SQL query built from an interpolated string and materializes the first row.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first row.</returns>
    ValueTask<TResult> FirstAsync<TResult>(DataSqlInterpolatedStringHandler statement, CancellationToken cancellationToken = default) =>
        FirstAsync<TResult>(statement.Build(), cancellationToken);

    /// <summary>
    /// Executes one SQL query and materializes the first row when present.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first row or <see langword="default"/>.</returns>
    ValueTask<TResult?> FirstOrDefaultAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one SQL query built from an interpolated string and materializes the first row when present.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first row or <see langword="default"/>.</returns>
    ValueTask<TResult?> FirstOrDefaultAsync<TResult>(DataSqlInterpolatedStringHandler statement, CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync<TResult>(statement.Build(), cancellationToken);

    /// <summary>
    /// Executes one SQL query and materializes one row.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The single row.</returns>
    ValueTask<TResult> SingleAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one SQL query built from an interpolated string and materializes one row.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The single row.</returns>
    ValueTask<TResult> SingleAsync<TResult>(DataSqlInterpolatedStringHandler statement, CancellationToken cancellationToken = default) =>
        SingleAsync<TResult>(statement.Build(), cancellationToken);

    /// <summary>
    /// Executes one SQL query and materializes one row when present.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The single row or <see langword="default"/>.</returns>
    ValueTask<TResult?> SingleOrDefaultAsync<TResult>(DataSqlStatement statement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one SQL query built from an interpolated string and materializes one row when present.
    /// </summary>
    /// <typeparam name="TResult">The row result type.</typeparam>
    /// <param name="statement">The SQL statement handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The single row or <see langword="default"/>.</returns>
    ValueTask<TResult?> SingleOrDefaultAsync<TResult>(DataSqlInterpolatedStringHandler statement, CancellationToken cancellationToken = default) =>
        SingleOrDefaultAsync<TResult>(statement.Build(), cancellationToken);
}
