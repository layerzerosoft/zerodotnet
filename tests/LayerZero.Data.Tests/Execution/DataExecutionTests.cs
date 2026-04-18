using LayerZero.Data.Internal.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Tests.Execution;

public sealed class DataExecutionTests
{
    [Fact]
    public async Task Dispatcher_executes_registered_queries_and_mutations()
    {
        await using var provider = TestDataServices.BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDataDispatcher>();

        var greeting = await dispatcher.QueryAsync(new CurrentAssemblyGreetingQuery("LayerZero"), TestContext.Current.CancellationToken);
        var appended = await dispatcher.MutateAsync(new CurrentAssemblyGreetingMutation("LayerZero"), TestContext.Current.CancellationToken);

        Assert.Equal("Hello LayerZero", greeting);
        Assert.Equal("Hello LayerZero!", appended);
    }

    [Fact]
    public void Raw_sql_handler_parameterizes_interpolated_values()
    {
        var id = Guid.NewGuid();
        var statement = CreateSqlStatement($"select * from orders where id = {id} and total > {12.5m}");

        Assert.Equal("select * from orders where id = @p0 and total > @p1", statement.CommandText);
        Assert.Equal(2, statement.Parameters.Count);
        Assert.Equal(id, statement.Parameters[0].Value);
    }

    [Fact]
    public void Command_cache_reuses_instances_for_same_key()
    {
        var cache = new DataCommandCache();
        var created = 0;

        var first = cache.GetOrAdd("reader:key", () =>
        {
            created++;
            return new object();
        });
        var second = cache.GetOrAdd("reader:key", () =>
        {
            created++;
            return new object();
        });

        Assert.Same(first, second);
        Assert.Equal(1, created);
    }

    private static DataSqlStatement CreateSqlStatement(DataSqlInterpolatedStringHandler sql) => sql.Build();
}
