using System.Text;
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

}
