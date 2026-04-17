using System.Data;
using System.Linq.Expressions;
using LayerZero.Data.Internal.Execution;
using LayerZero.Data.Internal.Materialization;
using LayerZero.Data.Internal.Registration;
using LayerZero.Data.Internal.Sql;
using LayerZero.Data.Internal.Translation;
using LayerZero.Data.SqlServer;
using LayerZero.Data.SqlServer.Configuration;
using LayerZero.Data.SqlServer.Internal.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.Tests;

public sealed class DataDslAndMaterializationTests
{
    [Fact]
    public void Query_translation_renders_expected_sql()
    {
        var model = DataQueryModel.Create<Order>()
            .AddFilter((Expression<Func<Order, bool>>)(order => order.CustomerEmail == "alice@example.com" && order.Total > 10m))
            .AddOrdering((Expression<Func<Order, DateTimeOffset>>)(order => order.CreatedAt), descending: true)
            .WithTake(5);

        Expression<Func<Order, OrderSummary>> projection = order => new OrderSummary(order.Id, order.CustomerEmail);

        var registry = CreateRegistry();
        var template = DataCommandTranslation.CreateReaderTemplate<OrderSummary>(model, projection, registry);
        var dialect = CreateDialect();
        var compiled = dialect.CompileReader(template, DataReadMode.List);

        Assert.Contains("select top (5)", compiled.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[customer_email]", compiled.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("order by [t0].[created_at] desc", compiled.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, compiled.Parameters.Count);
    }

    [Fact]
    public void Insert_parameter_collection_uses_json_converter()
    {
        var order = new Order(
            Guid.NewGuid(),
            "alice@example.com",
            42.5m,
            IsPaid: false,
            DateTimeOffset.UtcNow,
            new OrderMetadata("standard", 3));

        var table = new OrderMap().Table;
        var parameters = DataCommandTranslation.CollectInsertParameterValues(order, table);

        Assert.Contains(parameters, value => value is string text && text.Contains("\"priority\":3", StringComparison.Ordinal));
    }

    [Fact]
    public void Materializer_source_uses_constructor_binding_and_json_converter()
    {
        var registry = CreateRegistry();
        var materializers = new DataMaterializerSource(registry);

        var table = new DataTable();
        table.Columns.Add("id", typeof(Guid));
        table.Columns.Add("customer_email", typeof(string));
        table.Columns.Add("total", typeof(decimal));
        table.Columns.Add("is_paid", typeof(bool));
        table.Columns.Add("created_at", typeof(DateTimeOffset));
        table.Columns.Add("metadata", typeof(string));
        table.Rows.Add(
            Guid.NewGuid(),
            "alice@example.com",
            12.5m,
            true,
            DateTimeOffset.UtcNow,
            """{"channel":"priority","priority":7}""");

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var materializer = materializers.GetMaterializer<Order>(["id", "customer_email", "total", "is_paid", "created_at", "metadata"]);
        var order = materializer(reader);

        Assert.Equal("alice@example.com", order.CustomerEmail);
        Assert.Equal(7, order.Metadata.Priority);
        Assert.True(order.IsPaid);
    }

    [Fact]
    public async Task Dispatcher_executes_registered_queries_and_mutations()
    {
        var services = new ServiceCollection();
        services.AddLayerZeroData(options => options.ConnectionStringName = "Default")
            .UseSqlServer(options =>
            {
                options.ConnectionString = "Server=(local);Database=fake;User Id=sa;Password=Password1!;";
                options.DefaultSchema = "dbo";
            });
        services.AddTransient<IDataQueryHandler<GetGreetingQuery, string>, GetGreetingQueryHandler>();
        services.AddTransient<IDataMutationHandler<AppendGreetingMutation, string>, AppendGreetingMutationHandler>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDataDispatcher>();

        var greeting = await dispatcher.QueryAsync(new GetGreetingQuery("LayerZero"), TestContext.Current.CancellationToken);
        var appended = await dispatcher.MutateAsync(new AppendGreetingMutation("LayerZero"), TestContext.Current.CancellationToken);

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

    private static IEntityMapRegistry CreateRegistry() => new EntityMapRegistry([new OrderMap()]);

    private static SqlServerDataSqlDialect CreateDialect() =>
        new(Options.Create(new SqlServerDataOptions
        {
            ConnectionString = "Server=(local);Database=fake;User Id=sa;Password=Password1!;",
            ConnectionStringName = "Default",
            DefaultSchema = "dbo",
        }));

    private sealed record Order(
        Guid Id,
        string CustomerEmail,
        decimal Total,
        bool IsPaid,
        DateTimeOffset CreatedAt,
        OrderMetadata Metadata);

    private sealed record OrderMetadata(string Channel, int Priority);

    private sealed record OrderSummary(Guid Id, string CustomerEmail);

    private sealed class OrderMap : EntityMap<Order>
    {
        protected override void Configure(EntityMapBuilder<Order> builder)
        {
            builder.ToTable("orders");
            builder.Property(order => order.Id).HasColumnName("id").IsKeyPart();
            builder.Property(order => order.CustomerEmail).HasColumnName("customer_email").HasStringType(200).IsRequired();
            builder.Property(order => order.Total).HasColumnName("total").HasDecimalType(18, 2);
            builder.Property(order => order.IsPaid).HasColumnName("is_paid");
            builder.Property(order => order.CreatedAt).HasColumnName("created_at");
            builder.Property(order => order.Metadata).HasColumnName("metadata").HasJsonConversion().IsRequired();
        }
    }

    private sealed record GetGreetingQuery(string Name) : IDataQuery<string>;

    private sealed class GetGreetingQueryHandler : IDataQueryHandler<GetGreetingQuery, string>
    {
        public ValueTask<string> HandleAsync(GetGreetingQuery query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult($"Hello {query.Name}");
    }

    private sealed record AppendGreetingMutation(string Name) : IDataMutation<string>;

    private sealed class AppendGreetingMutationHandler : IDataMutationHandler<AppendGreetingMutation, string>
    {
        public ValueTask<string> HandleAsync(AppendGreetingMutation mutation, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult($"Hello {mutation.Name}!");
    }
}
