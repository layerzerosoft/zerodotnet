using Microsoft.Extensions.Logging;

namespace LayerZero.Bootstrap.Internal;

internal sealed class LayerZeroBootstrapRunner(
    LayerZeroBootstrapRegistry registry,
    IServiceProvider services,
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider)
{
    private readonly LayerZeroBootstrapRegistry registry = registry;
    private readonly IServiceProvider services = services;
    private readonly ILogger logger = loggerFactory.CreateLogger("LayerZero.Bootstrap");
    private readonly TimeProvider timeProvider = timeProvider;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var bootstrapStartedAt = timeProvider.GetTimestamp();

            foreach (var step in registry.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                logger.LogInformation("LayerZero bootstrap step '{StepName}' started.", step.Name);
                var stepStartedAt = timeProvider.GetTimestamp();

                await step.Execute(services, cancellationToken).ConfigureAwait(false);

                var stepElapsed = timeProvider.GetElapsedTime(stepStartedAt);
                logger.LogInformation(
                    "LayerZero bootstrap step '{StepName}' completed in {ElapsedMilliseconds}ms.",
                    step.Name,
                    stepElapsed.TotalMilliseconds);
            }

            var elapsed = timeProvider.GetElapsedTime(bootstrapStartedAt);
            logger.LogInformation(
                "LayerZero bootstrap completed successfully in {ElapsedMilliseconds}ms.",
                elapsed.TotalMilliseconds);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("LayerZero bootstrap cancelled.");
            return 1;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "LayerZero bootstrap failed.");
            return 1;
        }
    }
}
