using LayerZero.Core;

namespace LayerZero.Messaging.IntegrationTesting;

public sealed class HappyCommandHandler(IntegrationState state, IMessageContextAccessor accessor, IEventPublisher publisher) : ICommandHandler<HappyCommand>
{
    public async ValueTask<Result> HandleAsync(HappyCommand command, CancellationToken cancellationToken = default)
    {
        state.RecordInvocation("command.happy", accessor.Current!, GetType().FullName!);
        return await publisher.PublishAsync(new HappyEvent(command.OrderId, command.Payload), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class HappyAuditProjection(IntegrationState state, IMessageContextAccessor accessor) : IEventHandler<HappyEvent>
{
    public ValueTask<Result> HandleAsync(HappyEvent message, CancellationToken cancellationToken = default)
    {
        state.RecordInvocation("event.happy.audit", accessor.Current!, GetType().FullName!);
        return ValueTask.FromResult(Result.Success());
    }
}

public sealed class HappyAnalyticsProjection(IntegrationState state, IMessageContextAccessor accessor) : IEventHandler<HappyEvent>
{
    public ValueTask<Result> HandleAsync(HappyEvent message, CancellationToken cancellationToken = default)
    {
        state.RecordInvocation("event.happy.analytics", accessor.Current!, GetType().FullName!);
        return ValueTask.FromResult(Result.Success());
    }
}

public sealed class RetryCommandHandler(IntegrationState state, IMessageContextAccessor accessor) : ICommandHandler<RetryCommand>
{
    public ValueTask<Result> HandleAsync(RetryCommand command, CancellationToken cancellationToken = default)
    {
        state.RecordInvocation("command.retry", accessor.Current!, GetType().FullName!);
        var attempt = state.Increment($"retry:{command.OrderId:N}");
        if (attempt == 1)
        {
            throw new TimeoutException("Simulated transient retry failure.");
        }

        return ValueTask.FromResult(Result.Success());
    }
}

public sealed class PoisonCommandHandler(IntegrationState state, IMessageContextAccessor accessor) : ICommandHandler<PoisonCommand>
{
    public ValueTask<Result> HandleAsync(PoisonCommand command, CancellationToken cancellationToken = default)
    {
        state.RecordInvocation("command.poison", accessor.Current!, GetType().FullName!);
        throw new InvalidOperationException("Simulated poison command failure.");
    }
}

public sealed class PoisonAuditProjection(IntegrationState state, IMessageContextAccessor accessor) : IEventHandler<PoisonEvent>
{
    public ValueTask<Result> HandleAsync(PoisonEvent message, CancellationToken cancellationToken = default)
    {
        state.RecordInvocation("event.poison.audit", accessor.Current!, GetType().FullName!);
        return ValueTask.FromResult(Result.Success());
    }
}

public sealed class PoisonProjection(IntegrationState state, IMessageContextAccessor accessor) : IEventHandler<PoisonEvent>
{
    public ValueTask<Result> HandleAsync(PoisonEvent message, CancellationToken cancellationToken = default)
    {
        state.RecordInvocation("event.poison.projection", accessor.Current!, GetType().FullName!);
        throw new FormatException("Simulated poison event failure.");
    }
}

[IdempotentHandler]
public sealed class IdempotentCommandHandler(IntegrationState state, IMessageContextAccessor accessor) : ICommandHandler<IdempotentCommand>
{
    public ValueTask<Result> HandleAsync(IdempotentCommand command, CancellationToken cancellationToken = default)
    {
        state.RecordInvocation("command.idempotent", accessor.Current!, GetType().FullName!);
        state.Increment($"idempotent-side-effect:{command.OrderId:N}");
        return ValueTask.FromResult(Result.Success());
    }
}

public sealed class RestartCommandHandler(IntegrationState state, IMessageContextAccessor accessor) : ICommandHandler<RestartCommand>
{
    public async ValueTask<Result> HandleAsync(RestartCommand command, CancellationToken cancellationToken = default)
    {
        state.RecordInvocation("command.restart", accessor.Current!, GetType().FullName!);
        var invocation = state.Increment($"restart:{command.OrderId:N}");
        if (invocation == 1)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}
