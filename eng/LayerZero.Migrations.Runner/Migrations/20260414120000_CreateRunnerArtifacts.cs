namespace LayerZero.Migrations.Runner;

internal sealed class CreateRunnerArtifactsMigration : Migration
{
    /// <inheritdoc />
    public override void Build(MigrationBuilder builder)
    {
        builder.CreateTable("runner_artifacts", table =>
        {
            table.Column("id").AsInt32().Identity().NotNull();
            table.Column("name").AsString(128).NotNull();
            table.PrimaryKey("id");
        });
    }
}
