using LayerZero.Fulfillment.Shared;
using LayerZero.Migrations;

namespace LayerZero.Fulfillment.Bootstrap;

internal sealed class CreateFulfillmentTablesMigration : Migration
{
    public override void Build(MigrationBuilder builder)
    {
        var orders = new FulfillmentOrderMap().Table;
        var timeline = new FulfillmentTimelineMap().Table;
        var idempotency = new FulfillmentMessageIdempotencyMap().Table;
        var deadletters = new FulfillmentDeadLetterMap().Table;
        var scenarioFlags = new FulfillmentScenarioFlagMap().Table;
        var sideEffects = new FulfillmentSideEffectMap().Table;

        builder.EnsureSchema("public");
        builder.CreateTable(orders);
        builder.CreateTable(timeline);
        builder.CreateIndex(timeline, timeline.Indexes[0]);
        builder.CreateTable(idempotency);
        builder.CreateTable(deadletters);
        builder.CreateIndex(deadletters, deadletters.Indexes[0]);
        builder.CreateTable(scenarioFlags);
        builder.CreateTable(sideEffects);
    }
}
