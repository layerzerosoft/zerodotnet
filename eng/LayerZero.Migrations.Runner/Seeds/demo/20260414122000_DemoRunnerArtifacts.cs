namespace LayerZero.Migrations.Runner;

internal sealed class DemoRunnerArtifactsSeed : Seed
{
    /// <inheritdoc />
    public override void Build(SeedBuilder builder)
    {
        builder.UpsertData("runner_artifacts", ["id"], row => row.Set("id", 2).Set("name", "demo"));
    }
}
