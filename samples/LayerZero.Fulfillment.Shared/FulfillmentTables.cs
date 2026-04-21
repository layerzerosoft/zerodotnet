using LayerZero.Data;

namespace LayerZero.Fulfillment.Shared;

internal static class FulfillmentTables
{
    public static EntityTable<FulfillmentOrderRecord> Orders { get; } = new FulfillmentOrderMap().Table;

    public static EntityTable<FulfillmentTimelineRecord> Timeline { get; } = new FulfillmentTimelineMap().Table;

    public static EntityTable<FulfillmentScenarioFlagRecord> ScenarioFlags { get; } = new FulfillmentScenarioFlagMap().Table;

    public static EntityTable<FulfillmentSideEffectRecord> SideEffects { get; } = new FulfillmentSideEffectMap().Table;
}
