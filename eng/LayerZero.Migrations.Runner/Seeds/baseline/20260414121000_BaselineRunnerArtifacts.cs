namespace LayerZero.Migrations.Runner;

internal sealed class BaselineRunnerArtifactsSeed : Seed
{
    /// <inheritdoc />
    public override void Build(SeedBuilder builder)
    {
        builder.UpsertData("runner_artifacts", ["id"], row => row.Set("id", 1).Set("name", "baseline"));
    }
}
