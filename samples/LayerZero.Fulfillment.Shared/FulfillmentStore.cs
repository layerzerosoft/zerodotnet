using System.Text;
using LayerZero.Core;
using LayerZero.Data;
using LayerZero.Fulfillment.Contracts.Orders;
using LayerZero.Messaging;

namespace LayerZero.Fulfillment.Shared;

public sealed class FulfillmentStore(
    IDataContext dataContext,
    IMessageContextAccessor? messageContextAccessor = null,
    IMessageRegistry? messageRegistry = null,
    IMessageConventions? messageConventions = null)
{
    private readonly IDataContext dataContext = dataContext;
    private readonly IMessageContextAccessor? messageContextAccessor = messageContextAccessor;
    private readonly IMessageRegistry? messageRegistry = messageRegistry;
    private readonly IMessageConventions? messageConventions = messageConventions;

    public async Task CreateDraftOrderAsync(PlaceOrder command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var scope = await dataContext.BeginScopeAsync(cancellationToken).ConfigureAwait(false);

        await dataContext.InsertAsync(
            new FulfillmentOrderRecord(
                command.OrderId,
                command.CustomerEmail,
                OrderStatuses.Draft,
                InventoryReserved: false,
                PaymentAuthorized: false,
                CancelRequested: false,
                TrackingNumber: null,
                command.Items,
                command.ShippingAddress,
                command.Scenario),
            cancellationToken).ConfigureAwait(false);

        await dataContext.InsertAsync(
            new FulfillmentScenarioFlagRecord(
                command.OrderId,
                PaymentTimeoutConsumed: false),
            cancellationToken).ConfigureAwait(false);

        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OrderDetails?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await dataContext.Query<FulfillmentOrderRecord>()
            .Where(candidate => candidate.OrderId == orderId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return order is null
            ? null
            : ToOrderDetails(order);
    }

    public async Task<IReadOnlyList<OrderTimelineEntry>> GetTimelineAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var entries = await dataContext.Query<FulfillmentTimelineRecord>()
            .Where(entry => entry.OrderId == orderId)
            .OrderBy(entry => entry.Sequence)
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entries.Select(ToTimelineEntry).ToArray();
    }

    public async Task<IReadOnlyList<DeadLetterRecord>> GetDeadLettersAsync(CancellationToken cancellationToken = default)
    {
        var records = await dataContext.Query<FulfillmentDeadLetterRecord>()
            .OrderByDescending(entry => entry.FailedAtUtc)
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.Select(ToDeadLetterRecord).ToArray();
    }

    public async Task<(string TransportName, byte[] Body)?> GetDeadLetterEnvelopeAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var query = dataContext.Query<FulfillmentDeadLetterRecord>()
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
            : (envelope.TransportName, Convert.FromBase64String(envelope.BodyBase64));
    }

    public Task MarkDeadLetterRequeuedAsync(
        string messageId,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var update = dataContext.Update<FulfillmentDeadLetterRecord>()
            .Where(entry => entry.MessageId == messageId);

        if (!string.IsNullOrWhiteSpace(handlerIdentity))
        {
            update = update.Where(entry => entry.HandlerIdentity == handlerIdentity);
        }

        return update
            .Set(entry => entry.Requeued, true)
            .ExecuteAsync(cancellationToken)
            .AsTask();
    }

    public Task AppendTimelineAsync(
        Guid orderId,
        string step,
        string detail,
        string actor,
        string? handlerIdentity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        var context = messageContextAccessor?.Current;
        var entry = new FulfillmentTimelineRecord(
            Sequence: default,
            OrderId: orderId,
            Step: step,
            Detail: detail,
            Actor: actor,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            MessageId: context?.MessageId,
            HandlerIdentity: handlerIdentity,
            Attempt: context?.Attempt,
            TransportName: context?.TransportName,
            EntityName: ResolveEntityName(context),
            CorrelationId: context?.CorrelationId,
            TraceParent: context?.TraceParent);

        return dataContext.InsertAsync(entry, cancellationToken).AsTask();
    }

    public Task UpdateOrderStatusAsync(
        Guid orderId,
        string status,
        bool? inventoryReserved = null,
        bool? paymentAuthorized = null,
        bool? cancelRequested = null,
        string? trackingNumber = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        return dataContext.Sql().ExecuteAsync(
                BuildStatusUpdateStatement(
                    orderId,
                    status,
                    inventoryReserved,
                    paymentAuthorized,
                    cancelRequested,
                    trackingNumber),
                cancellationToken)
            .AsTask();
    }

    public async Task<bool> TryConsumePaymentTimeoutAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var changed = await dataContext.Update<FulfillmentScenarioFlagRecord>()
            .Where(flag => flag.OrderId == orderId && flag.PaymentTimeoutConsumed == false)
            .Set(flag => flag.PaymentTimeoutConsumed, true)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return changed == 1;
    }

    public async Task<bool> TryRecordSideEffectAsync(string effectKey, string? value = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectKey);

        var changed = await dataContext.Sql().ExecuteAsync(
            $"""
            insert into public.side_effects(effect_key, value, recorded_at_utc)
            values({effectKey}, {value}, {DateTimeOffset.UtcNow})
            on conflict(effect_key) do nothing;
            """,
            cancellationToken).ConfigureAwait(false);

        return changed == 1;
    }

    public Task SaveDeadLetterAsync(
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

        return dataContext.Sql().ExecuteAsync(
                $"""
                insert into public.deadletters(
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
                    {context.MessageId},
                    {context.MessageName},
                    {handlerIdentity},
                    {transportName},
                    {entityName},
                    {context.Attempt},
                    {context.CorrelationId},
                    {context.TraceParent},
                    {reason ?? "LayerZero dead-lettered the message."},
                    {string.Join("; ", errors.Select(static error => error.Code))},
                    {Convert.ToBase64String(body.ToArray())},
                    {DateTimeOffset.UtcNow},
                    {false})
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
                    requeued = {false};
                """,
                cancellationToken)
            .AsTask();
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

    private static OrderDetails ToOrderDetails(FulfillmentOrderRecord order)
    {
        return new OrderDetails(
            order.OrderId,
            order.CustomerEmail,
            order.Status,
            order.InventoryReserved,
            order.PaymentAuthorized,
            order.CancelRequested,
            order.TrackingNumber,
            order.Items,
            order.ShippingAddress,
            order.Scenario);
    }

    private static OrderTimelineEntry ToTimelineEntry(FulfillmentTimelineRecord entry)
    {
        return new OrderTimelineEntry(
            entry.Sequence,
            entry.Step,
            entry.Detail,
            entry.Actor,
            entry.OccurredAtUtc,
            entry.MessageId,
            entry.HandlerIdentity,
            entry.Attempt,
            entry.TransportName,
            entry.EntityName,
            entry.CorrelationId,
            entry.TraceParent);
    }

    private static DeadLetterRecord ToDeadLetterRecord(FulfillmentDeadLetterRecord entry)
    {
        return new DeadLetterRecord(
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

    private static DataSqlStatement BuildStatusUpdateStatement(
        Guid orderId,
        string status,
        bool? inventoryReserved,
        bool? paymentAuthorized,
        bool? cancelRequested,
        string? trackingNumber)
    {
        var commandText = new StringBuilder(
            """
            update public.orders
            set status = __p0__
            """);
        var parameters = new List<DataSqlParameter>
        {
            new("__p0__", status),
        };

        AppendAssignment(commandText, parameters, "inventory_reserved", inventoryReserved);
        AppendAssignment(commandText, parameters, "payment_authorized", paymentAuthorized);
        AppendAssignment(commandText, parameters, "cancel_requested", cancelRequested);
        AppendAssignment(commandText, parameters, "tracking_number", trackingNumber);

        var orderIdToken = $"__p{parameters.Count}__";
        commandText.AppendLine();
        commandText.Append($"where order_id = {orderIdToken}");
        parameters.Add(new DataSqlParameter(orderIdToken, orderId));
        commandText.AppendLine();
        commandText.Append("  and status not in ('completed', 'cancelled');");

        return new DataSqlStatement(commandText.ToString(), parameters);
    }

    private static void AppendAssignment(
        StringBuilder commandText,
        ICollection<DataSqlParameter> parameters,
        string columnName,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        var token = $"__p{parameters.Count}__";
        commandText.AppendLine(",");
        commandText.Append($"    {columnName} = {token}");
        parameters.Add(new DataSqlParameter(token, value));
    }

    private sealed record DeadLetterEnvelopeRow(string TransportName, string BodyBase64);
}
