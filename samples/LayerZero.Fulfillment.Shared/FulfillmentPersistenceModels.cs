using LayerZero.Data;
using LayerZero.Fulfillment.Contracts.Orders;

namespace LayerZero.Fulfillment.Shared;

internal sealed record FulfillmentOrderRecord(
    Guid OrderId,
    string CustomerEmail,
    string Status,
    bool InventoryReserved,
    bool PaymentAuthorized,
    bool CancelRequested,
    string? TrackingNumber,
    IReadOnlyList<OrderItem> Items,
    ShippingAddress ShippingAddress,
    OrderScenario Scenario);

internal sealed class FulfillmentOrderMap : EntityMap<FulfillmentOrderRecord>
{
    protected override void Configure(EntityMapBuilder<FulfillmentOrderRecord> builder)
    {
        builder.ToTable("orders");
        builder.Property(order => order.OrderId).HasColumnName("order_id").IsKeyPart();
        builder.Property(order => order.CustomerEmail).HasColumnName("customer_email").HasStringType(320).IsRequired();
        builder.Property(order => order.Status).HasColumnName("status").HasStringType(64).IsRequired();
        builder.Property(order => order.InventoryReserved).HasColumnName("inventory_reserved").HasDefaultValue(false);
        builder.Property(order => order.PaymentAuthorized).HasColumnName("payment_authorized").HasDefaultValue(false);
        builder.Property(order => order.CancelRequested).HasColumnName("cancel_requested").HasDefaultValue(false);
        builder.Property(order => order.TrackingNumber).HasColumnName("tracking_number").HasStringType(64).IsOptional();
        builder.Property(order => order.Items).HasColumnName("items_json").HasJsonConversion().IsRequired();
        builder.Property(order => order.ShippingAddress).HasColumnName("shipping_json").HasJsonConversion().IsRequired();
        builder.Property(order => order.Scenario).HasColumnName("scenario_json").HasJsonConversion().IsRequired();
    }
}

internal sealed record FulfillmentTimelineRecord(
    long Sequence,
    Guid OrderId,
    string Step,
    string Detail,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    string? MessageId,
    string? HandlerIdentity,
    int? Attempt,
    string? TransportName,
    string? EntityName,
    string? CorrelationId,
    string? TraceParent);

internal sealed class FulfillmentTimelineMap : EntityMap<FulfillmentTimelineRecord>
{
    protected override void Configure(EntityMapBuilder<FulfillmentTimelineRecord> builder)
    {
        builder.ToTable("order_timeline");
        builder.Property(entry => entry.Sequence).HasColumnName("id").IsIdentity().IsKeyPart();
        builder.Property(entry => entry.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(entry => entry.Step).HasColumnName("step").HasStringType(128).IsRequired();
        builder.Property(entry => entry.Detail).HasColumnName("detail").HasStringType().IsRequired();
        builder.Property(entry => entry.Actor).HasColumnName("actor").HasStringType(64).IsRequired();
        builder.Property(entry => entry.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(entry => entry.MessageId).HasColumnName("message_id").HasStringType(64).IsOptional();
        builder.Property(entry => entry.HandlerIdentity).HasColumnName("handler_identity").HasStringType(256).IsOptional();
        builder.Property(entry => entry.Attempt).HasColumnName("attempt").IsOptional();
        builder.Property(entry => entry.TransportName).HasColumnName("transport_name").HasStringType(64).IsOptional();
        builder.Property(entry => entry.EntityName).HasColumnName("entity_name").HasStringType(256).IsOptional();
        builder.Property(entry => entry.CorrelationId).HasColumnName("correlation_id").HasStringType(64).IsOptional();
        builder.Property(entry => entry.TraceParent).HasColumnName("trace_parent").HasStringType(128).IsOptional();
        builder.HasIndex("IX_order_timeline_order_id_sequence", isUnique: false, entry => entry.OrderId, entry => entry.Sequence);
    }
}

internal sealed record FulfillmentMessageIdempotencyRecord(
    string DedupeKey,
    string Status,
    DateTimeOffset UpdatedAtUtc);

internal sealed class FulfillmentMessageIdempotencyMap : EntityMap<FulfillmentMessageIdempotencyRecord>
{
    protected override void Configure(EntityMapBuilder<FulfillmentMessageIdempotencyRecord> builder)
    {
        builder.ToTable("message_idempotency");
        builder.Property(entry => entry.DedupeKey).HasColumnName("dedupe_key").HasStringType(256).IsKeyPart();
        builder.Property(entry => entry.Status).HasColumnName("status").HasStringType(32).IsRequired();
        builder.Property(entry => entry.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
    }
}

internal sealed record FulfillmentDeadLetterRecord(
    string MessageId,
    string MessageName,
    string HandlerIdentity,
    string TransportName,
    string EntityName,
    int Attempt,
    string? CorrelationId,
    string? TraceParent,
    string Reason,
    string Errors,
    string BodyBase64,
    DateTimeOffset FailedAtUtc,
    bool Requeued);

internal sealed class FulfillmentDeadLetterMap : EntityMap<FulfillmentDeadLetterRecord>
{
    protected override void Configure(EntityMapBuilder<FulfillmentDeadLetterRecord> builder)
    {
        builder.ToTable("deadletters");
        builder.Property(entry => entry.MessageId).HasColumnName("message_id").HasStringType(64).IsKeyPart();
        builder.Property(entry => entry.MessageName).HasColumnName("message_name").HasStringType(256).IsRequired();
        builder.Property(entry => entry.HandlerIdentity).HasColumnName("handler_identity").HasStringType(256).IsKeyPart();
        builder.Property(entry => entry.TransportName).HasColumnName("transport_name").HasStringType(64).IsRequired();
        builder.Property(entry => entry.EntityName).HasColumnName("entity_name").HasStringType(256).IsRequired();
        builder.Property(entry => entry.Attempt).HasColumnName("attempt").IsRequired();
        builder.Property(entry => entry.CorrelationId).HasColumnName("correlation_id").HasStringType(64).IsOptional();
        builder.Property(entry => entry.TraceParent).HasColumnName("trace_parent").HasStringType(128).IsOptional();
        builder.Property(entry => entry.Reason).HasColumnName("reason").HasStringType().IsRequired();
        builder.Property(entry => entry.Errors).HasColumnName("errors").HasStringType().IsRequired();
        builder.Property(entry => entry.BodyBase64).HasColumnName("body_base64").HasStringType().IsRequired();
        builder.Property(entry => entry.FailedAtUtc).HasColumnName("failed_at_utc").IsRequired();
        builder.Property(entry => entry.Requeued).HasColumnName("requeued").HasDefaultValue(false);
        builder.HasIndex("IX_deadletters_failed_at_utc", isUnique: false, entry => entry.FailedAtUtc);
    }
}

internal sealed record FulfillmentScenarioFlagRecord(
    Guid OrderId,
    bool PaymentTimeoutConsumed);

internal sealed class FulfillmentScenarioFlagMap : EntityMap<FulfillmentScenarioFlagRecord>
{
    protected override void Configure(EntityMapBuilder<FulfillmentScenarioFlagRecord> builder)
    {
        builder.ToTable("scenario_flags");
        builder.Property(entry => entry.OrderId).HasColumnName("order_id").IsKeyPart();
        builder.Property(entry => entry.PaymentTimeoutConsumed).HasColumnName("payment_timeout_consumed").HasDefaultValue(false);
    }
}

internal sealed record FulfillmentSideEffectRecord(
    string EffectKey,
    string? Value,
    DateTimeOffset RecordedAtUtc);

internal sealed class FulfillmentSideEffectMap : EntityMap<FulfillmentSideEffectRecord>
{
    protected override void Configure(EntityMapBuilder<FulfillmentSideEffectRecord> builder)
    {
        builder.ToTable("side_effects");
        builder.Property(entry => entry.EffectKey).HasColumnName("effect_key").HasStringType(256).IsKeyPart();
        builder.Property(entry => entry.Value).HasColumnName("value").HasStringType().IsOptional();
        builder.Property(entry => entry.RecordedAtUtc).HasColumnName("recorded_at_utc").IsRequired();
    }
}
