using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Internal.Execution;

internal sealed class DataDispatcher(IServiceProvider services) : IDataDispatcher
{
    public ValueTask<TResult> QueryAsync<TResult>(IDataQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return QueryInvokerCache<TResult>.Get(query.GetType())(services, query, cancellationToken);
    }

    public ValueTask<TResult> MutateAsync<TResult>(IDataMutation<TResult> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        return MutationInvokerCache<TResult>.Get(mutation.GetType())(services, mutation, cancellationToken);
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

    private static class QueryInvokerCache<TResult>
    {
        private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDataQuery<TResult>, CancellationToken, ValueTask<TResult>>> Cache = new();
        private static readonly MethodInfo QueryCoreMethod = typeof(DataDispatcher)
            .GetMethod(nameof(QueryCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetGenericMethodDefinition();

        public static Func<IServiceProvider, IDataQuery<TResult>, CancellationToken, ValueTask<TResult>> Get(Type queryType)
        {
            ArgumentNullException.ThrowIfNull(queryType);
            return Cache.GetOrAdd(queryType, static currentType =>
                (Func<IServiceProvider, IDataQuery<TResult>, CancellationToken, ValueTask<TResult>>)QueryCoreMethod
                    .MakeGenericMethod(currentType, typeof(TResult))
                    .CreateDelegate(typeof(Func<IServiceProvider, IDataQuery<TResult>, CancellationToken, ValueTask<TResult>>)));
        }
    }

    private static class MutationInvokerCache<TResult>
    {
        private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDataMutation<TResult>, CancellationToken, ValueTask<TResult>>> Cache = new();
        private static readonly MethodInfo MutationCoreMethod = typeof(DataDispatcher)
            .GetMethod(nameof(MutationCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetGenericMethodDefinition();

        public static Func<IServiceProvider, IDataMutation<TResult>, CancellationToken, ValueTask<TResult>> Get(Type mutationType)
        {
            ArgumentNullException.ThrowIfNull(mutationType);
            return Cache.GetOrAdd(mutationType, static currentType =>
                (Func<IServiceProvider, IDataMutation<TResult>, CancellationToken, ValueTask<TResult>>)MutationCoreMethod
                    .MakeGenericMethod(currentType, typeof(TResult))
                    .CreateDelegate(typeof(Func<IServiceProvider, IDataMutation<TResult>, CancellationToken, ValueTask<TResult>>)));
        }
    }
}
