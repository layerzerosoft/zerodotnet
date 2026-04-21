using LayerZero.Messaging.Operations.Postgres.Internal;
using LayerZero.Migrations;

namespace LayerZero.Messaging.Operations.Postgres;

internal sealed class CreateMessagingOperationsTablesMigration : Migration
{
    public override void Build(MigrationBuilder builder)
    {
        builder.CreateTable(PostgresMessagingOperationsTables.MessageIdempotency);
        builder.CreateTable(PostgresMessagingOperationsTables.DeadLetters);
        builder.CreateIndex(
            PostgresMessagingOperationsTables.DeadLetters,
            PostgresMessagingOperationsTables.DeadLetters.Indexes[0]);
    }
}
