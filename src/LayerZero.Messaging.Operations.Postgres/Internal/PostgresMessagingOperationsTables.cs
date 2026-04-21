using LayerZero.Data;

namespace LayerZero.Messaging.Operations.Postgres.Internal;

internal static class PostgresMessagingOperationsTables
{
    public const string IdempotencyTableName = "lz_message_idempotency";
    public const string DeadLettersTableName = "lz_dead_letters";

    public static EntityTable<MessagingOperationIdempotencyRecord> MessageIdempotency { get; } =
        new MessagingOperationIdempotencyMap().Table;

    public static EntityTable<MessagingOperationDeadLetterRecord> DeadLetters { get; } =
        new MessagingOperationDeadLetterMap().Table;
}
