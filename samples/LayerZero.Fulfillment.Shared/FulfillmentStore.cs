using System.Text.Json;
using LayerZero.Core;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Messaging;
using Microsoft.Data.Sqlite;

namespace LayerZero.Fulfillment.Shared;

public sealed class FulfillmentStore
{
    private readonly string connectionString;
    private readonly IMessageContextAccessor? messageContextAccessor;
    private readonly IMessageRegistry? messageRegistry;
    private readonly IMessageConventions? messageConventions;

    public FulfillmentStore(
        string connectionString,
        IMessageContextAccessor? messageContextAccessor = null,
        IMessageRegistry? messageRegistry = null,
        IMessageConventions? messageConventions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
        this.messageContextAccessor = messageContextAccessor;
        this.messageRegistry = messageRegistry;
        this.messageConventions = messageConventions;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists orders(
                order_id text primary key,
                customer_email text not null,
                status text not null,
                inventory_reserved integer not null default 0,
                payment_authorized integer not null default 0,
                cancel_requested integer not null default 0,
                tracking_number text null,
                items_json text not null,
                shipping_json text not null,
                scenario_json text not null
            );

            create table if not exists order_timeline(
                id integer primary key autoincrement,
                order_id text not null,
                step text not null,
                detail text not null,
                actor text not null,
                occurred_at_utc text not null,
                message_id text null,
                handler_identity text null,
                attempt integer null,
                transport_name text null,
                entity_name text null,
                correlation_id text null,
                trace_parent text null
            );

            create table if not exists message_idempotency(
                dedupe_key text primary key,
                status text not null,
                updated_at_utc text not null
            );

            create table if not exists deadletters(
                message_id text not null,
                message_name text not null,
                handler_identity text not null,
                transport_name text not null,
                entity_name text not null,
                attempt integer not null default 0,
                correlation_id text null,
                trace_parent text null,
                reason text not null,
                errors text not null,
                body_base64 text not null,
                failed_at_utc text not null,
                requeued integer not null default 0,
                primary key(message_id, handler_identity)
            );

            create table if not exists scenario_flags(
                order_id text primary key,
                payment_timeout_consumed integer not null default 0
            );

            create table if not exists side_effects(
                effect_key text primary key,
                value text null,
                recorded_at_utc text not null
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await EnsureDeadLettersTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "order_timeline", "message_id", "text null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "order_timeline", "handler_identity", "text null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "order_timeline", "attempt", "integer null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "order_timeline", "transport_name", "text null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "order_timeline", "entity_name", "text null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "order_timeline", "correlation_id", "text null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "order_timeline", "trace_parent", "text null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "deadletters", "entity_name", "text not null default ''", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "deadletters", "attempt", "integer not null default 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "deadletters", "correlation_id", "text null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "deadletters", "trace_parent", "text null", cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateDraftOrderAsync(PlaceOrder command, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var insert = connection.CreateCommand();
        insert.CommandText = """
            insert into orders(order_id, customer_email, status, items_json, shipping_json, scenario_json)
            values($orderId, $customerEmail, $status, $items, $shipping, $scenario)
            on conflict(order_id) do nothing;
            """;
        insert.Parameters.AddWithValue("$orderId", command.OrderId.ToString("N"));
        insert.Parameters.AddWithValue("$customerEmail", command.CustomerEmail);
        insert.Parameters.AddWithValue("$status", OrderStatuses.Draft);
        insert.Parameters.AddWithValue("$items", JsonSerializer.Serialize(command.Items));
        insert.Parameters.AddWithValue("$shipping", JsonSerializer.Serialize(command.ShippingAddress));
        insert.Parameters.AddWithValue("$scenario", JsonSerializer.Serialize(command.Scenario));
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var scenario = connection.CreateCommand();
        scenario.CommandText = """
            insert into scenario_flags(order_id, payment_timeout_consumed)
            values($orderId, 0)
            on conflict(order_id) do nothing;
            """;
        scenario.Parameters.AddWithValue("$orderId", command.OrderId.ToString("N"));
        await scenario.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OrderDetails?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            select customer_email, status, inventory_reserved, payment_authorized, cancel_requested, tracking_number, items_json, shipping_json, scenario_json
            from orders where order_id = $orderId;
            """;
        command.Parameters.AddWithValue("$orderId", orderId.ToString("N"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new OrderDetails(
            orderId,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2) == 1,
            reader.GetInt64(3) == 1,
            reader.GetInt64(4) == 1,
            reader.IsDBNull(5) ? null : reader.GetString(5),
            JsonSerializer.Deserialize<IReadOnlyList<OrderItem>>(reader.GetString(6)) ?? [],
            JsonSerializer.Deserialize<ShippingAddress>(reader.GetString(7))!,
            JsonSerializer.Deserialize<OrderScenario>(reader.GetString(8))!);
    }

    public async Task<IReadOnlyList<OrderTimelineEntry>> GetTimelineAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var entries = new List<OrderTimelineEntry>();
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            select id, step, detail, actor, occurred_at_utc, message_id, handler_identity, attempt, transport_name, entity_name, correlation_id, trace_parent
            from order_timeline
            where order_id = $orderId
            order by id asc;
            """;
        command.Parameters.AddWithValue("$orderId", orderId.ToString("N"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new OrderTimelineEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11)));
        }

        return entries;
    }

    public async Task<IReadOnlyList<DeadLetterRecord>> GetDeadLettersAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<DeadLetterRecord>();
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            select message_id, message_name, handler_identity, transport_name, entity_name, attempt, correlation_id, trace_parent, reason, errors, failed_at_utc, requeued
            from deadletters
            order by failed_at_utc desc;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(new DeadLetterRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                DateTimeOffset.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetInt64(11) == 1));
        }

        return records;
    }

    public async Task<(string TransportName, byte[] Body)?> GetDeadLetterEnvelopeAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrWhiteSpace(handlerIdentity)
            ? "select transport_name, body_base64 from deadletters where message_id = $messageId order by failed_at_utc desc limit 1;"
            : "select transport_name, body_base64 from deadletters where message_id = $messageId and handler_identity = $handlerIdentity;";
        command.Parameters.AddWithValue("$messageId", messageId);
        if (!string.IsNullOrWhiteSpace(handlerIdentity))
        {
            command.Parameters.AddWithValue("$handlerIdentity", handlerIdentity);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (reader.GetString(0), Convert.FromBase64String(reader.GetString(1)));
    }

    public async Task MarkDeadLetterRequeuedAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrWhiteSpace(handlerIdentity)
            ? "update deadletters set requeued = 1 where message_id = $messageId;"
            : "update deadletters set requeued = 1 where message_id = $messageId and handler_identity = $handlerIdentity;";
        command.Parameters.AddWithValue("$messageId", messageId);
        if (!string.IsNullOrWhiteSpace(handlerIdentity))
        {
            command.Parameters.AddWithValue("$handlerIdentity", handlerIdentity);
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendTimelineAsync(
        Guid orderId,
        string step,
        string detail,
        string actor,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        var context = messageContextAccessor?.Current;
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            insert into order_timeline(
                order_id,
                step,
                detail,
                actor,
                occurred_at_utc,
                message_id,
                handler_identity,
                attempt,
                transport_name,
                entity_name,
                correlation_id,
                trace_parent)
            values(
                $orderId,
                $step,
                $detail,
                $actor,
                $occurredAtUtc,
                $messageId,
                $handlerIdentity,
                $attempt,
                $transportName,
                $entityName,
                $correlationId,
                $traceParent);
            """;
        command.Parameters.AddWithValue("$orderId", orderId.ToString("N"));
        command.Parameters.AddWithValue("$step", step);
        command.Parameters.AddWithValue("$detail", detail);
        command.Parameters.AddWithValue("$actor", actor);
        command.Parameters.AddWithValue("$occurredAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$messageId", (object?)context?.MessageId ?? DBNull.Value);
        command.Parameters.AddWithValue("$handlerIdentity", (object?)handlerIdentity ?? DBNull.Value);
        command.Parameters.AddWithValue("$attempt", context is null ? DBNull.Value : context.Attempt);
        command.Parameters.AddWithValue("$transportName", (object?)context?.TransportName ?? DBNull.Value);
        command.Parameters.AddWithValue("$entityName", (object?)ResolveEntityName(context) ?? DBNull.Value);
        command.Parameters.AddWithValue("$correlationId", (object?)context?.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$traceParent", (object?)context?.TraceParent ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateOrderStatusAsync(
        Guid orderId,
        string status,
        bool? inventoryReserved = null,
        bool? paymentAuthorized = null,
        bool? cancelRequested = null,
        string? trackingNumber = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            update orders
            set status = case
                    when status in ('completed', 'cancelled') then status
                    else $status
                end,
                inventory_reserved = coalesce($inventoryReserved, inventory_reserved),
                payment_authorized = coalesce($paymentAuthorized, payment_authorized),
                cancel_requested = coalesce($cancelRequested, cancel_requested),
                tracking_number = coalesce($trackingNumber, tracking_number)
            where order_id = $orderId;
            """;
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$inventoryReserved", ToDbValue(inventoryReserved));
        command.Parameters.AddWithValue("$paymentAuthorized", ToDbValue(paymentAuthorized));
        command.Parameters.AddWithValue("$cancelRequested", ToDbValue(cancelRequested));
        command.Parameters.AddWithValue("$trackingNumber", (object?)trackingNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$orderId", orderId.ToString("N"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryConsumePaymentTimeoutAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            update scenario_flags
            set payment_timeout_consumed = 1
            where order_id = $orderId and payment_timeout_consumed = 0;
            """;
        command.Parameters.AddWithValue("$orderId", orderId.ToString("N"));
        var changed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return changed == 1;
    }

    public async Task<bool> TryRecordSideEffectAsync(string effectKey, string? value = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectKey);

        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            insert into side_effects(effect_key, value, recorded_at_utc)
            values($effectKey, $value, $recordedAtUtc)
            on conflict(effect_key) do nothing;
            """;
        command.Parameters.AddWithValue("$effectKey", effectKey);
        command.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
        command.Parameters.AddWithValue("$recordedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task SaveDeadLetterAsync(
        MessageContext context,
        string handlerIdentity,
        string transportName,
        string entityName,
        IReadOnlyList<Error> errors,
        string? reason,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = """
            insert into deadletters(message_id, message_name, handler_identity, transport_name, entity_name, attempt, correlation_id, trace_parent, reason, errors, body_base64, failed_at_utc, requeued)
            values($messageId, $messageName, $handlerIdentity, $transportName, $entityName, $attempt, $correlationId, $traceParent, $reason, $errors, $body, $failedAtUtc, 0)
            on conflict(message_id, handler_identity) do update set
                handler_identity = excluded.handler_identity,
                transport_name = excluded.transport_name,
                entity_name = excluded.entity_name,
                attempt = excluded.attempt,
                correlation_id = excluded.correlation_id,
                trace_parent = excluded.trace_parent,
                reason = excluded.reason,
                errors = excluded.errors,
                body_base64 = excluded.body_base64,
                failed_at_utc = excluded.failed_at_utc,
                requeued = 0;
            """;
        command.Parameters.AddWithValue("$messageId", context.MessageId);
        command.Parameters.AddWithValue("$messageName", context.MessageName);
        command.Parameters.AddWithValue("$handlerIdentity", handlerIdentity);
        command.Parameters.AddWithValue("$transportName", transportName);
        command.Parameters.AddWithValue("$entityName", entityName);
        command.Parameters.AddWithValue("$attempt", context.Attempt);
        command.Parameters.AddWithValue("$correlationId", (object?)context.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$traceParent", (object?)context.TraceParent ?? DBNull.Value);
        command.Parameters.AddWithValue("$reason", reason ?? "LayerZero dead-lettered the message.");
        command.Parameters.AddWithValue("$errors", string.Join("; ", errors.Select(static error => error.Code)));
        command.Parameters.AddWithValue("$body", Convert.ToBase64String(body.ToArray()));
        command.Parameters.AddWithValue("$failedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public SqliteConnection OpenConnection() => new(connectionString);

    private static object ToDbValue(bool? value)
    {
        return value is null ? DBNull.Value : value.Value ? 1 : 0;
    }

    private string? ResolveEntityName(MessageContext? context)
    {
        if (context is null || messageRegistry is null || messageConventions is null)
        {
            return null;
        }

        return messageRegistry.TryGetDescriptor(context.MessageName, out var descriptor)
            ? messageConventions.GetEntityName(descriptor)
            : null;
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        var pragma = connection.CreateCommand();
        pragma.CommandText = $"pragma table_info({tableName});";

        await using (var reader = await pragma.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        var alter = connection.CreateCommand();
        alter.CommandText = $"alter table {tableName} add column {columnName} {columnDefinition};";
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureDeadLettersTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var tableInfo = await GetTableInfoAsync(connection, "deadletters", cancellationToken).ConfigureAwait(false);
        if (tableInfo.Count == 0)
        {
            return;
        }

        if (tableInfo.TryGetValue("message_id", out var messageIdPrimaryKeyOrder)
            && tableInfo.TryGetValue("handler_identity", out var handlerIdentityPrimaryKeyOrder)
            && messageIdPrimaryKeyOrder > 0
            && handlerIdentityPrimaryKeyOrder > 0
            && tableInfo.ContainsKey("entity_name")
            && tableInfo.ContainsKey("attempt")
            && tableInfo.ContainsKey("correlation_id")
            && tableInfo.ContainsKey("trace_parent")
            && tableInfo.ContainsKey("requeued"))
        {
            return;
        }

        await EnsureColumnAsync(connection, "deadletters", "handler_identity", "text not null default ''", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "deadletters", "entity_name", "text not null default ''", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "deadletters", "attempt", "integer not null default 0", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "deadletters", "correlation_id", "text null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "deadletters", "trace_parent", "text null", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "deadletters", "requeued", "integer not null default 0", cancellationToken).ConfigureAwait(false);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var create = connection.CreateCommand();
        create.Transaction = transaction;
        create.CommandText = """
            create table deadletters_new(
                message_id text not null,
                message_name text not null,
                handler_identity text not null,
                transport_name text not null,
                entity_name text not null,
                attempt integer not null default 0,
                correlation_id text null,
                trace_parent text null,
                reason text not null,
                errors text not null,
                body_base64 text not null,
                failed_at_utc text not null,
                requeued integer not null default 0,
                primary key(message_id, handler_identity)
            );
            """;
        await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var copy = connection.CreateCommand();
        copy.Transaction = transaction;
        copy.CommandText = """
            insert into deadletters_new(
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
            select
                message_id,
                message_name,
                handler_identity,
                transport_name,
                coalesce(entity_name, ''),
                coalesce(attempt, 0),
                correlation_id,
                trace_parent,
                reason,
                errors,
                body_base64,
                failed_at_utc,
                coalesce(requeued, 0)
            from deadletters;
            """;
        await copy.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var drop = connection.CreateCommand();
        drop.Transaction = transaction;
        drop.CommandText = "drop table deadletters;";
        await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var rename = connection.CreateCommand();
        rename.Transaction = transaction;
        rename.CommandText = "alter table deadletters_new rename to deadletters;";
        await rename.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, int>> GetTableInfoAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"pragma table_info({tableName});";

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns[reader.GetString(1)] = reader.GetInt32(5);
        }

        return columns;
    }
}
