using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Internal.Execution;

internal sealed class DataDispatcher(IServiceProvider services) : IDataDispatcher
{
    private static readonly MethodInfo QueryCoreMethod = typeof(DataDispatcher)
        .GetMethod(nameof(QueryCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo MutationCoreMethod = typeof(DataDispatcher)
        .GetMethod(nameof(MutationCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public ValueTask<TResult> QueryAsync<TResult>(IDataQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var closed = QueryCoreMethod.MakeGenericMethod(query.GetType(), typeof(TResult));
        return (ValueTask<TResult>)closed.Invoke(obj: null, [services, query, cancellationToken])!;
    }

    public ValueTask<TResult> MutateAsync<TResult>(IDataMutation<TResult> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        var closed = MutationCoreMethod.MakeGenericMethod(mutation.GetType(), typeof(TResult));
        return (ValueTask<TResult>)closed.Invoke(obj: null, [services, mutation, cancellationToken])!;
    }

    private static ValueTask<TResult> QueryCoreAsync<TQuery, TResult>(
        IServiceProvider services,
        IDataQuery<TResult> query,
        CancellationToken cancellationToken)
        where TQuery : IDataQuery<TResult>
    {
        return services.GetRequiredService<IDataQueryHandler<TQuery, TResult>>()
            .HandleAsync((TQuery)query, cancellationToken);
    }

    private static ValueTask<TResult> MutationCoreAsync<TMutation, TResult>(
        IServiceProvider services,
        IDataMutation<TResult> mutation,
        CancellationToken cancellationToken)
        where TMutation : IDataMutation<TResult>
    {
        return services.GetRequiredService<IDataMutationHandler<TMutation, TResult>>()
            .HandleAsync((TMutation)mutation, cancellationToken);
    }
}
