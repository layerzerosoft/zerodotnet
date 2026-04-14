using System.Diagnostics;
using LayerZero.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayerZero.Messaging.IntegrationTesting;

public abstract class MessageTransportIntegrationTestBase
{
    public static bool SkipWhenCloudEnvironmentUnavailable => false;

    protected virtual string BusName => "primary";

    protected abstract string BrokerName { get; }

    protected abstract IHost CreateHost(string applicationName, IntegrationState? state = null);

    [OptionalCloudEnvironmentFact]
    public async Task Commands_publish_events_and_propagate_trace_metadata()
    {
        using var host = CreateHost(CreateApplicationName());
        await IntegrationTestHost.ProvisionAsync(host, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await IntegrationTestHost.ValidateAsync(host, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        var state = host.Services.GetRequiredService<IntegrationState>();
        var sender = host.Services.GetRequiredService<ICommandSender>();

        using var activity = new Activity("integration-happy-flow").Start();
        var orderId = Guid.NewGuid();
        var result = await sender.SendAsync(new HappyCommand(orderId, "hello"), TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.True(result.IsSuccess);

        await state.WaitForAsync(
            static current => current.Count("command.happy") == 1
                && current.Count("event.happy.audit") == 1
                && current.Count("event.happy.analytics") == 1,
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        var commandInvocation = Assert.Single(state.Invocations.Where(static invocation => invocation.Marker == "command.happy"));
        var eventInvocations = state.Invocations.Where(static invocation => invocation.MessageName == MessageNames.For<HappyEvent>()).ToArray();

        Assert.Equal(orderId.ToString("N"), commandInvocation.AffinityKey);
        Assert.All(eventInvocations, invocation =>
        {
            Assert.Equal(commandInvocation.CorrelationId, invocation.CorrelationId);
            Assert.Equal(commandInvocation.TraceParent, invocation.TraceParent);
            Assert.Equal(orderId.ToString("N"), invocation.AffinityKey);
            Assert.Equal(BusName, invocation.TransportName);
        });
    }

    [OptionalCloudEnvironmentFact]
    public async Task Retryable_failures_complete_before_the_retry_budget_is_exhausted()
    {
        using var host = CreateHost(CreateApplicationName());
        await IntegrationTestHost.ProvisionAsync(host, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        var state = host.Services.GetRequiredService<IntegrationState>();
        var sender = host.Services.GetRequiredService<ICommandSender>();
        var orderId = Guid.NewGuid();

        var result = await sender.SendAsync(new RetryCommand(orderId, "retry"), TestContext.Current.CancellationToken).ConfigureAwait(false);
        Assert.True(result.IsSuccess);

        await state.WaitForAsync(
            current => current.Count("command.retry") >= 2
                && current.Settlements.Any(settlement => settlement.MessageName == MessageNames.For<RetryCommand>() && settlement.Action == MessageProcessingAction.Complete),
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.Equal(2, state.Count("command.retry"));
        Assert.Contains(state.Settlements, settlement => settlement.MessageName == MessageNames.For<RetryCommand>() && settlement.Action == MessageProcessingAction.Retry);
        Assert.Contains(state.Settlements, settlement => settlement.MessageName == MessageNames.For<RetryCommand>() && settlement.Action == MessageProcessingAction.Complete);
    }

    [OptionalCloudEnvironmentFact]
    public async Task Terminal_failures_are_dead_lettered_after_the_retry_budget_is_exhausted()
    {
        using var host = CreateHost(CreateApplicationName());
        await IntegrationTestHost.ProvisionAsync(host, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        var state = host.Services.GetRequiredService<IntegrationState>();
        var sender = host.Services.GetRequiredService<ICommandSender>();
        var orderId = Guid.NewGuid();

        var result = await sender.SendAsync(new PoisonCommand(orderId, "poison"), TestContext.Current.CancellationToken).ConfigureAwait(false);
        Assert.True(result.IsSuccess);

        await state.WaitForAsync(
            current => current.Settlements.Any(settlement =>
                settlement.MessageName == MessageNames.For<PoisonCommand>()
                && settlement.Action == MessageProcessingAction.DeadLetter),
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.Contains(state.Settlements, settlement => settlement.MessageName == MessageNames.For<PoisonCommand>() && settlement.Action == MessageProcessingAction.DeadLetter);
        Assert.True(state.Count("command.poison") >= 2);
    }

    [OptionalCloudEnvironmentFact]
    public async Task Duplicate_delivery_executes_idempotent_handlers_only_once()
    {
        using var host = CreateHost(CreateApplicationName());
        await IntegrationTestHost.ProvisionAsync(host, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        var state = host.Services.GetRequiredService<IntegrationState>();
        var registry = host.Services.GetRequiredService<IMessageRegistry>();
        var serializer = host.Services.GetRequiredService<MessageEnvelopeSerializer>();
        var transport = host.Services.GetRequiredKeyedService<IMessageBusTransport>("primary");

        Assert.True(registry.TryGetDescriptor(typeof(IdempotentCommand), out var descriptor));

        var orderId = Guid.NewGuid();
        var command = new IdempotentCommand(orderId, "dedupe");
        var context = new MessageContext(
            Guid.NewGuid().ToString("N"),
            descriptor.Name,
            descriptor.Kind,
            BusName,
            Guid.NewGuid().ToString("N"),
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            0,
            orderId.ToString("N"));
        var body = serializer.Serialize(descriptor, command, context);
        var message = new TransportMessage(descriptor, context, body);

        await transport.SendAsync(message, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await transport.SendAsync(message, TestContext.Current.CancellationToken).ConfigureAwait(false);

        await state.WaitForAsync(
            current => current.Count($"idempotent-side-effect:{orderId:N}") == 1,
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.Equal(1, state.Count("command.idempotent"));
        Assert.Equal(1, state.Count($"idempotent-side-effect:{orderId:N}"));
    }

    [OptionalCloudEnvironmentFact]
    public async Task Topology_validation_requires_explicit_provisioning()
    {
        using var host = CreateHost(CreateApplicationName());

        var validationFailure = await Record.ExceptionAsync(
            () => IntegrationTestHost.ValidateAsync(host, TestContext.Current.CancellationToken)).ConfigureAwait(false);

        Assert.NotNull(validationFailure);

        await IntegrationTestHost.ProvisionAsync(host, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await IntegrationTestHost.ValidateAsync(host, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    [OptionalCloudEnvironmentFact]
    public async Task Restarting_the_consumer_redelivers_unsettled_messages()
    {
        var applicationName = CreateApplicationName();
        var sharedState = new IntegrationState();

        var host = CreateHost(applicationName, sharedState);
        try
        {
            await IntegrationTestHost.ProvisionAsync(host, TestContext.Current.CancellationToken).ConfigureAwait(false);
            await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

            var sender = host.Services.GetRequiredService<ICommandSender>();
            var orderId = Guid.NewGuid();
            var result = await sender.SendAsync(new RestartCommand(orderId, "restart"), TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.True(result.IsSuccess);

            await sharedState.WaitForAsync(
                current => current.Count("command.restart") == 1,
                TimeSpan.FromSeconds(30),
                TestContext.Current.CancellationToken).ConfigureAwait(false);

            await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            await DisposeHostAsync(host).ConfigureAwait(false);
        }
        catch
        {
            await DisposeHostAsync(host).ConfigureAwait(false);
            throw;
        }

        using var restartedHost = CreateHost(applicationName, sharedState);
        await restartedHost.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        await sharedState.WaitForAsync(
            current => current.Count("command.restart") >= 2
                && current.Settlements.Any(settlement => settlement.MessageName == MessageNames.For<RestartCommand>() && settlement.Action == MessageProcessingAction.Complete),
            TimeSpan.FromSeconds(30),
            TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private string CreateApplicationName()
    {
        return $"{BrokerName.ToLowerInvariant()}-{Guid.NewGuid():N}";
    }

    private static async ValueTask DisposeHostAsync(IHost host)
    {
        switch (host)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
