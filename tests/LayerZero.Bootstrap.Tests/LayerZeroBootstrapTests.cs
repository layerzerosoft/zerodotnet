using LayerZero.Bootstrap;
using LayerZero.Bootstrap.Messaging;
using LayerZero.Bootstrap.Migrations;
using LayerZero.Messaging;
using LayerZero.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LayerZero.Bootstrap.Tests;

public sealed class LayerZeroBootstrapTests
{
    [Fact]
    public async Task Runner_executes_steps_in_registration_order_and_stops_after_the_first_failure()
    {
        var builder = Host.CreateApplicationBuilder();
        var state = new StepState();
        builder.Services.AddSingleton(state);
        builder.AddLayerZeroBootstrap(bootstrap => bootstrap
            .AddStep(
                "first",
                static (services, _) =>
                {
                    services.GetRequiredService<StepState>().ExecutedSteps.Add("first");
                    return ValueTask.CompletedTask;
                })
            .AddStep(
                "second",
                static (services, _) =>
                {
                    services.GetRequiredService<StepState>().ExecutedSteps.Add("second");
                    throw new InvalidOperationException("The second step failed.");
                })
            .AddStep(
                "third",
                static (services, _) =>
                {
                    services.GetRequiredService<StepState>().ExecutedSteps.Add("third");
                    return ValueTask.CompletedTask;
                }));

        var exitCode = await builder.RunLayerZeroBootstrapAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal(["first", "second"], state.ExecutedSteps);
    }

    [Fact]
    public async Task Command_routing_returns_null_without_consuming_the_single_host_build_when_no_command_matches()
    {
        var builder = Host.CreateApplicationBuilder();
        var state = new StepState();
        builder.Services.AddSingleton(state);
        builder.AddLayerZeroBootstrap(bootstrap => bootstrap
            .AddCommandHandler(static (_, _, _, _) => Task.FromResult<int?>(null))
            .AddStep(
                "only-step",
                static (services, _) =>
                {
                    services.GetRequiredService<StepState>().ExecutedSteps.Add("only-step");
                    return ValueTask.CompletedTask;
                }));

        var commandExitCode = await builder.RunLayerZeroBootstrapCommandsAsync(["noop"], TestContext.Current.CancellationToken);
        var bootstrapExitCode = await builder.RunLayerZeroBootstrapAsync(TestContext.Current.CancellationToken);

        Assert.Null(commandExitCode);
        Assert.Equal(0, bootstrapExitCode);
        Assert.Equal(["only-step"], state.ExecutedSteps);
    }

    [Fact]
    public async Task Migrations_step_routes_existing_migration_commands()
    {
        var builder = Host.CreateApplicationBuilder();
        var runtime = new FakeMigrationRuntime();
        builder.Services.AddSingleton<IMigrationRuntime>(runtime);
        builder.AddLayerZeroBootstrap(bootstrap => bootstrap.AddMigrationsStep());

        var exitCode = await builder.RunLayerZeroBootstrapCommandsAsync(
            ["migrations", "apply"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, runtime.ApplyCalls);
    }

    [Fact]
    public async Task Messaging_step_routes_existing_messaging_commands()
    {
        var builder = Host.CreateApplicationBuilder();
        var provisioner = new FakeTopologyProvisioner();
        builder.Services.AddSingleton<IMessageTopologyProvisioner>(provisioner);
        builder.AddLayerZeroBootstrap(bootstrap => bootstrap.AddMessagingProvisioningStep());

        var exitCode = await builder.RunLayerZeroBootstrapCommandsAsync(
            ["messaging", "provision"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, provisioner.ProvisionCalls);
    }

    [Fact]
    public async Task Runner_logs_step_lifecycle_and_total_completion()
    {
        var builder = Host.CreateApplicationBuilder();
        var loggerProvider = new CapturingLoggerProvider();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(loggerProvider);

        builder.AddLayerZeroBootstrap(bootstrap => bootstrap.AddStep(
            "migrations",
            static (_, _) => ValueTask.CompletedTask));

        var exitCode = await builder.RunLayerZeroBootstrapAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(loggerProvider.Messages, static message => message.Contains("LayerZero bootstrap step 'migrations' started.", StringComparison.Ordinal));
        Assert.Contains(loggerProvider.Messages, static message => message.Contains("LayerZero bootstrap step 'migrations' completed in", StringComparison.Ordinal));
        Assert.Contains(loggerProvider.Messages, static message => message.Contains("LayerZero bootstrap completed successfully in", StringComparison.Ordinal));
    }

    private sealed class StepState
    {
        public List<string> ExecutedSteps { get; } = [];
    }

    private sealed class FakeMigrationRuntime : IMigrationRuntime
    {
        public int ApplyCalls { get; private set; }

        public ValueTask<MigrationInfoResult> InfoAsync(MigrationInfoOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<MigrationValidationResult> ValidateAsync(MigrationValidationOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<MigrationScriptResult> ScriptAsync(MigrationScriptOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<MigrationApplyResult> ApplyAsync(MigrationApplyOptions? options = null, CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            return ValueTask.FromResult(new MigrationApplyResult([], []));
        }

        public ValueTask<MigrationBaselineResult> BaselineAsync(MigrationBaselineOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeTopologyProvisioner : IMessageTopologyProvisioner
    {
        public int ProvisionCalls { get; private set; }

        public ValueTask ValidateAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask ProvisionAsync(CancellationToken cancellationToken = default)
        {
            ProvisionCalls++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> messages = [];

        public IReadOnlyList<string> Messages => messages;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        private readonly List<string> messages = messages;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }
}
