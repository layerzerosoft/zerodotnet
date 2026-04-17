using System.Data.Common;

namespace LayerZero.Data.Internal.Execution;

internal sealed class ActiveDataScope(DbConnection connection, DbTransaction transaction)
{
    public DbConnection Connection { get; } = connection;

    public DbTransaction Transaction { get; } = transaction;
}

internal sealed class DataScopeManager
{
    private ActiveDataScope? current;

    public ActiveDataScope? Current
    {
        get => current;
        set => current = value;
    }
}

internal sealed class DataScope(
    DataScopeManager scopeManager,
    ActiveDataScope activeScope) : IDataScope
{
    private bool disposed;
    private bool committed;

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (committed)
        {
            return;
        }

        committed = true;
        await activeScope.Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        scopeManager.Current = null;

        try
        {
            if (!committed)
            {
                await activeScope.Transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await activeScope.Transaction.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await activeScope.Connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}

internal sealed class JoinedDataScope : IDataScope
{
    public static JoinedDataScope Instance { get; } = new();

    public ValueTask CommitAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
