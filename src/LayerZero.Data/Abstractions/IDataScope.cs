namespace LayerZero.Data;

/// <summary>
/// Represents one explicit LayerZero data scope.
/// </summary>
public interface IDataScope : IAsyncDisposable
{
    /// <summary>
    /// Commits the active scope.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed operation when the scope is committed.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
}
