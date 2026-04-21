using LayerZero.Data;

namespace LayerZero.Messaging.Operations.Postgres.Internal;

internal sealed record MessagingOperationIdempotencyRecord(
    string DedupeKey,
    string Status,
    DateTimeOffset UpdatedAtUtc);

internal sealed class MessagingOperationIdempotencyMap : EntityMap<MessagingOperationIdempotencyRecord>
{
    protected override void Configure(EntityMapBuilder<MessagingOperationIdempotencyRecord> builder)
    {
        builder.ToTable("lz_message_idempotency");
        builder.Property(entry => entry.DedupeKey).HasColumnName("dedupe_key").HasStringType(256).IsKeyPart();
        builder.Property(entry => entry.Status).HasColumnName("status").HasStringType(32).IsRequired();
        builder.Property(entry => entry.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
    }
}

internal sealed record MessagingOperationDeadLetterRecord(
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

internal sealed class MessagingOperationDeadLetterMap : EntityMap<MessagingOperationDeadLetterRecord>
{
    protected override void Configure(EntityMapBuilder<MessagingOperationDeadLetterRecord> builder)
    {
        builder.ToTable("lz_dead_letters");
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
        builder.HasIndex("IX_lz_dead_letters_failed_at_utc", isUnique: false, entry => entry.FailedAtUtc);
    }
}
