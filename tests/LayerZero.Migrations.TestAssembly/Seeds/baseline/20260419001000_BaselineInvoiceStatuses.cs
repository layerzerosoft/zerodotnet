using LayerZero.Migrations;

namespace LayerZero.Migrations.TestAssembly.Seeds.Baseline;

public sealed class BaselineInvoiceStatusesSeed : Seed
{
    public override void Build(SeedBuilder builder)
    {
        builder.InsertData("invoice_statuses", rows =>
        {
            rows.Row(row => row.Set("id", 1).Set("name", "pending"));
        });
    }
}
