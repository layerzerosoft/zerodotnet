namespace LayerZero.Migrations.Runner;

internal sealed class CreateRunnerArtifactsMigration : Migration
{
    internal CreateRunnerArtifactsMigration()
        : base("20260414120000", "Create runner artifacts table")
    {
    }

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

internal sealed class BaselineRunnerSeed : Seed
{
    internal BaselineRunnerSeed()
        : base("20260414121000", "Baseline runner artifact")
    {
    }

    public override void Build(SeedBuilder builder)
    {
        builder.UpsertData("runner_artifacts", ["id"], row => row.Set("id", 1).Set("name", "baseline"));
    }
}

internal sealed class DemoRunnerSeed : Seed
{
    internal DemoRunnerSeed()
        : base("20260414122000", "Demo runner artifact", "demo")
    {
    }

    public override void Build(SeedBuilder builder)
    {
        builder.UpsertData("runner_artifacts", ["id"], row => row.Set("id", 2).Set("name", "demo"));
    }
}
