using System.Runtime.Loader;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace LayerZero.Messaging.IntegrationTesting;

public abstract class TestcontainerFixtureBase<TContainer> : IAsyncLifetime, IAsyncDisposable
    where TContainer : class
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly TestcontainerFixtureMetadata metadata;
    private EventHandler? processExitHandler;
    private Action<AssemblyLoadContext>? unloadingHandler;
    private int initializeState;
    private int disposeState;

    protected TestcontainerFixtureBase(string projectName, string brokerName)
    {
        metadata = new TestcontainerFixtureMetadata(projectName, brokerName);
    }

    protected TestcontainerFixtureMetadata Metadata => metadata;

    public TContainer Container { get; private set; } = null!;

    public IReadOnlyDictionary<string, string> Labels => metadata.Labels;

    public string RunId => metadata.RunId;

    public async Task<IReadOnlyDictionary<string, string>> GetContainerLabelsAsync(CancellationToken cancellationToken = default)
    {
        var details = await GetContainerDetailsAsync(Container).ConfigureAwait(false);
        return await TestcontainerDockerInspector.GetLabelsAsync(details.ContainerId, cancellationToken).ConfigureAwait(false);
    }

    protected internal void TriggerShutdownCleanupForTests()
    {
        BestEffortDispose();
    }

    public async ValueTask InitializeAsync()
    {
        if (Interlocked.CompareExchange(ref initializeState, 1, 0) != 0)
        {
            return;
        }

        await lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            RegisterShutdownHooks();
            Container = await CreateContainerAsync(metadata).ConfigureAwait(false);
            await StartContainerAsync(Container, CancellationToken.None).ConfigureAwait(false);

            var details = await GetContainerDetailsAsync(Container).ConfigureAwait(false);
            await TestcontainerFixtureLogging.LogStartedContainerAsync(
                metadata.ProjectName,
                metadata.BrokerName,
                metadata.RunId,
                details.ContainerId,
                details.ContainerName).ConfigureAwait(false);
        }
        catch
        {
            await CleanupCreatedContainerAsync(CancellationToken.None).ConfigureAwait(false);
            UnregisterShutdownHooks();
            Volatile.Write(ref disposeState, 1);
            throw;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeCoreAsync(CancellationToken.None).ConfigureAwait(false);
    }

    protected TBuilder ApplyContainerDefaults<TBuilder, TResource, TConfiguration>(IAbstractBuilder<TBuilder, TResource, TConfiguration> builder)
        where TBuilder : IAbstractBuilder<TBuilder, TResource, TConfiguration>
    {
        ArgumentNullException.ThrowIfNull(builder);

        var configuredBuilder = builder.WithCleanUp(true);
        foreach (var label in metadata.Labels)
        {
            configuredBuilder = configuredBuilder.WithLabel(label.Key, label.Value);
        }

        return configuredBuilder;
    }

    protected virtual ValueTask<TContainer> CreateContainerAsync(TestcontainerFixtureMetadata metadata)
    {
        throw new NotImplementedException();
    }

    protected virtual Task StartContainerAsync(TContainer container, CancellationToken cancellationToken)
    {
        return container switch
        {
            IContainer dockerContainer => dockerContainer.StartAsync(cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    protected virtual Task StopContainerAsync(TContainer container, CancellationToken cancellationToken)
    {
        return container switch
        {
            IContainer dockerContainer => dockerContainer.StopAsync(cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    protected virtual ValueTask DisposeContainerAsync(TContainer container)
    {
        return container switch
        {
            IAsyncDisposable asyncDisposable => asyncDisposable.DisposeAsync(),
            IDisposable disposable => DisposeSync(disposable),
            _ => ValueTask.CompletedTask,
        };
    }

    protected virtual ValueTask<ContainerDetails> GetContainerDetailsAsync(TContainer container)
    {
        return container switch
        {
            IContainer dockerContainer => ValueTask.FromResult(new ContainerDetails(dockerContainer.Id, dockerContainer.Name)),
            _ => ValueTask.FromResult(new ContainerDetails("unknown", "unknown")),
        };
    }

    private static ValueTask DisposeSync(IDisposable disposable)
    {
        disposable.Dispose();
        return ValueTask.CompletedTask;
    }

    private void RegisterShutdownHooks()
    {
        processExitHandler = (_, _) => BestEffortDispose();
        unloadingHandler = _ => BestEffortDispose();
        AppDomain.CurrentDomain.ProcessExit += processExitHandler;
        AssemblyLoadContext.Default.Unloading += unloadingHandler;
    }

    private void UnregisterShutdownHooks()
    {
        if (processExitHandler is not null)
        {
            AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
            processExitHandler = null;
        }

        if (unloadingHandler is not null)
        {
            AssemblyLoadContext.Default.Unloading -= unloadingHandler;
            unloadingHandler = null;
        }
    }

    private void BestEffortDispose()
    {
        try
        {
            DisposeCoreAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
        catch
        {
        }
    }

    private async ValueTask DisposeCoreAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref disposeState, 1, 0) != 0)
        {
            return;
        }

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            UnregisterShutdownHooks();

            if (Container is not null)
            {
                try
                {
                    await StopContainerAsync(Container, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                }

                try
                {
                    await DisposeContainerAsync(Container).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    private async ValueTask CleanupCreatedContainerAsync(CancellationToken cancellationToken)
    {
        if (Container is null)
        {
            return;
        }

        try
        {
            await StopContainerAsync(Container, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            await DisposeContainerAsync(Container).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    protected internal readonly record struct ContainerDetails(string ContainerId, string ContainerName);
}
