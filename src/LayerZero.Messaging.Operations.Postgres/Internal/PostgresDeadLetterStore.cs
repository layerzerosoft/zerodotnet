using LayerZero.Core;
using LayerZero.Data;
using LayerZero.Data.Postgres.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LayerZero.Messaging.Operations.Postgres.Internal;

internal sealed class PostgresDeadLetterStore(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<PostgresDataOptions> dataOptionsAccessor) : IDeadLetterStore
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private readonly TimeProvider timeProvider = timeProvider;
    private readonly PostgresDataOptions dataOptions = dataOptionsAccessor.Value;

    public async Task<IReadOnlyList<DeadLetterEntry>> GetDeadLettersAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var records = await dataContext.Query<MessagingOperationDeadLetterRecord>()
            .OrderByDescending(entry => entry.FailedAtUtc)
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.Select(ToEntry).ToArray();
    }

    public async Task<DeadLetterEnvelope?> GetEnvelopeAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();

        var query = dataContext.Query<MessagingOperationDeadLetterRecord>()
            .Where(entry => entry.MessageId == messageId);

        if (!string.IsNullOrWhiteSpace(handlerIdentity))
        {
            query = query.Where(entry => entry.HandlerIdentity == handlerIdentity);
        }

        var envelope = await query
            .OrderByDescending(entry => entry.FailedAtUtc)
            .Take(1)
            .Select(entry => new DeadLetterEnvelopeRow(entry.TransportName, entry.BodyBase64))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return envelope is null
            ? null
            : new DeadLetterEnvelope(envelope.TransportName, Convert.FromBase64String(envelope.BodyBase64));
    }

    public async Task MarkRequeuedAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();

        var update = dataContext.Update<MessagingOperationDeadLetterRecord>()
            .Where(entry => entry.MessageId == messageId);

        if (!string.IsNullOrWhiteSpace(handlerIdentity))
        {
            update = update.Where(entry => entry.HandlerIdentity == handlerIdentity);
        }

        await update
            .Set(entry => entry.Requeued, true)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task ArchiveAsync(
        MessageContext context,
        string handlerIdentity,
        string transportName,
        string entityName,
        IReadOnlyList<Error> errors,
        string? reason,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(errors);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        await dataContext.Sql().ExecuteAsync(
            BuildArchiveStatement(context, handlerIdentity, transportName, entityName, errors, reason, body),
            cancellationToken).ConfigureAwait(false);
    }

    private DataSqlStatement BuildArchiveStatement(
        MessageContext context,
        string handlerIdentity,
        string transportName,
        string entityName,
        IReadOnlyList<Error> errors,
        string? reason,
        ReadOnlyMemory<byte> body)
    {
        var table = FormatTable(PostgresMessagingOperationsTables.DeadLettersTableName);
        return new DataSqlStatement(
            $"""
            insert into {table}(
                message_id,
                message_name,
                handler_identity,
                transport_name,
                entity_name,
                attempt,
                correlation_id,
                trace_parent,
                reason,
                errors,
                body_base64,
                failed_at_utc,
                requeued)
            values(
                __p0__,
                __p1__,
                __p2__,
                __p3__,
                __p4__,
                __p5__,
                __p6__,
                __p7__,
                __p8__,
                __p9__,
                __p10__,
                __p11__,
                __p12__)
            on conflict(message_id, handler_identity) do update set
                transport_name = excluded.transport_name,
                entity_name = excluded.entity_name,
                attempt = excluded.attempt,
                correlation_id = excluded.correlation_id,
                trace_parent = excluded.trace_parent,
                reason = excluded.reason,
                errors = excluded.errors,
                body_base64 = excluded.body_base64,
                failed_at_utc = excluded.failed_at_utc,
                requeued = __p12__;
            """,
            [
                new DataSqlParameter("__p0__", context.MessageId),
                new DataSqlParameter("__p1__", context.MessageName),
                new DataSqlParameter("__p2__", handlerIdentity),
                new DataSqlParameter("__p3__", transportName),
                new DataSqlParameter("__p4__", entityName),
                new DataSqlParameter("__p5__", context.Attempt),
                new DataSqlParameter("__p6__", context.CorrelationId),
                new DataSqlParameter("__p7__", context.TraceParent),
                new DataSqlParameter("__p8__", reason ?? "LayerZero dead-lettered the message."),
                new DataSqlParameter("__p9__", string.Join("; ", errors.Select(static error => error.Code))),
                new DataSqlParameter("__p10__", Convert.ToBase64String(body.ToArray())),
                new DataSqlParameter("__p11__", timeProvider.GetUtcNow()),
                new DataSqlParameter("__p12__", false),
            ]);
    }

    private DeadLetterEntry ToEntry(MessagingOperationDeadLetterRecord entry)
    {
        return new DeadLetterEntry(
            entry.MessageId,
            entry.MessageName,
            entry.HandlerIdentity,
            entry.TransportName,
            entry.EntityName,
            entry.Attempt,
            entry.CorrelationId,
            entry.TraceParent,
            entry.Reason,
            entry.Errors,
            entry.FailedAtUtc,
            entry.Requeued);
    }

    private string FormatTable(string tableName)
    {
        return $"{QuoteIdentifier(dataOptions.DefaultSchema)}.{QuoteIdentifier(tableName)}";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private sealed record DeadLetterEnvelopeRow(string TransportName, string BodyBase64);
}
