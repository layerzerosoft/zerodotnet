using LayerZero.Data;
using LayerZero.Data.Postgres.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Operations.Postgres.Internal;

internal sealed class PostgresMessageIdempotencyStore(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<PostgresDataOptions> dataOptionsAccessor) : IMessageIdempotencyStore
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private readonly TimeProvider timeProvider = timeProvider;
    private readonly PostgresDataOptions dataOptions = dataOptionsAccessor.Value;

    public async ValueTask<bool> TryBeginAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var changed = await dataContext.Sql().ExecuteAsync(
            BuildTryBeginStatement(deduplicationKey),
            cancellationToken).ConfigureAwait(false);

        return changed == 1;
    }

    public async ValueTask CompleteAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        await dataContext.Sql().ExecuteAsync(
            BuildCompleteStatement(deduplicationKey),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AbandonAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        await dataContext.Sql().ExecuteAsync(
            BuildAbandonStatement(deduplicationKey),
            cancellationToken).ConfigureAwait(false);
    }

    private DataSqlStatement BuildTryBeginStatement(string deduplicationKey)
    {
        var table = FormatTable(PostgresMessagingOperationsTables.IdempotencyTableName);
        return new DataSqlStatement(
            $"""
            insert into {table}(dedupe_key, status, updated_at_utc)
            values(__p0__, __p1__, __p2__)
            on conflict(dedupe_key) do nothing;
            """,
            [
                new DataSqlParameter("__p0__", deduplicationKey),
                new DataSqlParameter("__p1__", "processing"),
                new DataSqlParameter("__p2__", timeProvider.GetUtcNow()),
            ]);
    }

    private DataSqlStatement BuildCompleteStatement(string deduplicationKey)
    {
        var table = FormatTable(PostgresMessagingOperationsTables.IdempotencyTableName);
        return new DataSqlStatement(
            $"""
            update {table}
            set status = __p0__,
                updated_at_utc = __p1__
            where dedupe_key = __p2__;
            """,
            [
                new DataSqlParameter("__p0__", "complete"),
                new DataSqlParameter("__p1__", timeProvider.GetUtcNow()),
                new DataSqlParameter("__p2__", deduplicationKey),
            ]);
    }

    private DataSqlStatement BuildAbandonStatement(string deduplicationKey)
    {
        var table = FormatTable(PostgresMessagingOperationsTables.IdempotencyTableName);
        return new DataSqlStatement(
            $"""
            delete from {table}
            where dedupe_key = __p0__;
            """,
            [
                new DataSqlParameter("__p0__", deduplicationKey),
            ]);
    }

    private string FormatTable(string tableName)
    {
        return $"{QuoteIdentifier(dataOptions.DefaultSchema)}.{QuoteIdentifier(tableName)}";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
