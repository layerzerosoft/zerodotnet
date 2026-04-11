using LayerZero.Testcontainers.Cleanup;

if (!CleanupArguments.TryParse(args, Console.Error, out var parsed))
{
    return 1;
}

try
{
    var runner = new CleanupRunner(
        new DockerCliResourceStore(new DockerProcessRunner()),
        Console.Out);

    return await runner.RunAsync(parsed, CancellationToken.None).ConfigureAwait(false);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
