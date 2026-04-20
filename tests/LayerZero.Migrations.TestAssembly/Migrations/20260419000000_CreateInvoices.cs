using LayerZero.Migrations;

namespace LayerZero.Migrations.TestAssembly.Migrations;

public sealed class CreateInvoicesMigration : Migration
{
    public override void Build(MigrationBuilder builder)
    {
        builder.CreateTable("invoices", table =>
        {
            table.Column("id").AsGuid().NotNull();
            table.Column("number").AsString(64).NotNull();
            table.PrimaryKey("id");
        });
    }
}
