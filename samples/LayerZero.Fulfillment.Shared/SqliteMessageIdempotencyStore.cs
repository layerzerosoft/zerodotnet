using LayerZero.Messaging;

namespace LayerZero.Fulfillment.Shared;

public sealed class SqliteMessageIdempotencyStore(FulfillmentStore store) : IMessageIdempotencyStore
{
    public async ValueTask<bool> TryBeginAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        await using var connection = store.OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            insert into message_idempotency(dedupe_key, status, updated_at_utc)
            values($dedupeKey, 'processing', $updatedAtUtc)
            on conflict(dedupe_key) do nothing;
            """;
        command.Parameters.AddWithValue("$dedupeKey", deduplicationKey);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async ValueTask CompleteAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        await using var connection = store.OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "update message_idempotency set status = 'complete', updated_at_utc = $updatedAtUtc where dedupe_key = $dedupeKey;";
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$dedupeKey", deduplicationKey);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AbandonAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        await using var connection = store.OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "delete from message_idempotency where dedupe_key = $dedupeKey;";
        command.Parameters.AddWithValue("$dedupeKey", deduplicationKey);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
