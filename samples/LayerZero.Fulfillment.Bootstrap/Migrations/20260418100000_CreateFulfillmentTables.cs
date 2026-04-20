using LayerZero.Fulfillment.Shared;
using LayerZero.Migrations;

namespace LayerZero.Fulfillment.Bootstrap;

internal sealed class CreateFulfillmentTablesMigration : Migration
{
    public override void Build(MigrationBuilder builder)
    {
        builder.CreateTable(FulfillmentTables.Orders);
        builder.CreateTable(FulfillmentTables.Timeline);
        builder.CreateIndex(FulfillmentTables.Timeline, FulfillmentTables.Timeline.Indexes[0]);
        builder.CreateTable(FulfillmentTables.MessageIdempotency);
        builder.CreateTable(FulfillmentTables.DeadLetters);
        builder.CreateIndex(FulfillmentTables.DeadLetters, FulfillmentTables.DeadLetters.Indexes[0]);
        builder.CreateTable(FulfillmentTables.ScenarioFlags);
        builder.CreateTable(FulfillmentTables.SideEffects);
    }
}
