namespace LayerZero.Testcontainers.Cleanup;

internal enum DockerResourceKind
{
    Container = 0,
    Network = 1,
    Volume = 2,
}
