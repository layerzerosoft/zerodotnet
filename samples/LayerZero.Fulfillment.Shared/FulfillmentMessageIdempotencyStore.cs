using LayerZero.Data;
using LayerZero.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Fulfillment.Shared;

public sealed class FulfillmentMessageIdempotencyStore(IServiceScopeFactory scopeFactory) : IMessageIdempotencyStore
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;

    public async ValueTask<bool> TryBeginAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);

        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var changed = await dataContext.Sql().ExecuteAsync(
            $"""
            insert into public.message_idempotency(dedupe_key, status, updated_at_utc)
            values({deduplicationKey}, {"processing"}, {DateTimeOffset.UtcNow})
            on conflict(dedupe_key) do nothing;
            """,
            cancellationToken).ConfigureAwait(false);

        return changed == 1;
    }

    public async ValueTask CompleteAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);

        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        await dataContext.Sql().ExecuteAsync(
            $"""
            update public.message_idempotency
            set status = {"complete"},
                updated_at_utc = {DateTimeOffset.UtcNow}
            where dedupe_key = {deduplicationKey};
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AbandonAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);

        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        await dataContext.Sql().ExecuteAsync(
            $"""
            delete from public.message_idempotency
            where dedupe_key = {deduplicationKey};
            """,
            cancellationToken).ConfigureAwait(false);
    }
}
