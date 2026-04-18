using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LayerZero.Data.Postgres.IntegrationTests;

public sealed class PostgresDataContextIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16.4").Build();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        await CreateSchemaAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Direct_context_can_insert_query_update_delete_and_aggregate()
    {
        await ResetTablesAsync(TestContext.Current.CancellationToken);
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var data = scope.ServiceProvider.GetRequiredService<IDataContext>();

        var alice = new Order(Guid.NewGuid(), "alice@example.com", 12.5m, false, DateTimeOffset.UtcNow, new OrderMetadata("web", 1));
        var bob = new Order(Guid.NewGuid(), "bob@example.com", 15m, false, DateTimeOffset.UtcNow.AddMinutes(1), new OrderMetadata("mobile", 2));

        await data.InsertAsync(alice, TestContext.Current.CancellationToken);
        await data.InsertAsync(bob, TestContext.Current.CancellationToken);

        var summaries = await data.Query<Order>()
            .Where(order => order.IsPaid == false)
            .OrderBy(order => order.CustomerEmail)
            .Select(order => new OrderSummary(order.Id, order.CustomerEmail))
            .ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, summaries.Count);
        Assert.Equal(27.5m, await data.Query<Order>().SumAsync(order => order.Total, TestContext.Current.CancellationToken));
        Assert.Equal(2L, await data.Query<Order>().LongCountAsync(TestContext.Current.CancellationToken));
        Assert.True(await data.Query<Order>().AnyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(12.5m, await data.Query<Order>().MinAsync(order => order.Total, TestContext.Current.CancellationToken));
        Assert.Equal(15m, await data.Query<Order>().MaxAsync(order => order.Total, TestContext.Current.CancellationToken));

        var changed = await data.Update<Order>()
            .Where(order => order.CustomerEmail == "alice@example.com")
            .Set(order => order.IsPaid, true)
            .ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, changed);
        Assert.Equal(1, await data.Query<Order>().Where(order => order.IsPaid == true).CountAsync(TestContext.Current.CancellationToken));

        var deleted = await data.Delete<Order>()
            .Where(order => order.CustomerEmail == "bob@example.com")
            .ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, deleted);
        Assert.Equal(1, await data.Query<Order>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Direct_context_can_join_project_and_execute_raw_sql()
    {
        await ResetTablesAsync(TestContext.Current.CancellationToken);
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var data = scope.ServiceProvider.GetRequiredService<IDataContext>();

        await data.InsertAsync(new Customer(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "alice@example.com"), TestContext.Current.CancellationToken);
        await data.InsertAsync(new Order(Guid.NewGuid(), "alice@example.com", 12.5m, true, DateTimeOffset.UtcNow, new OrderMetadata("web", 1)), TestContext.Current.CancellationToken);

        var joined = await data.Query<Order>()
            .Join<Customer, string>(order => order.CustomerEmail, customer => customer.Email)
            .Where(row => row.Left.IsPaid == true)
            .Select(row => new JoinedOrderSummary(row.Left.CustomerEmail, row.Right.Email))
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("alice@example.com", joined.CustomerEmail);

        var raw = await data.Sql().SingleAsync<OrderSummary>(
            $"select id as \"Id\", customer_email as \"CustomerEmail\" from public.orders where customer_email = {"alice@example.com"}",
            TestContext.Current.CancellationToken);

        Assert.Equal("alice@example.com", raw.CustomerEmail);
    }

    [Fact]
    public async Task Explicit_scope_requires_commit_and_rolls_back_by_default()
    {
        await ResetTablesAsync(TestContext.Current.CancellationToken);
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var data = scope.ServiceProvider.GetRequiredService<IDataContext>();

        await using (var transaction = await data.BeginScopeAsync(TestContext.Current.CancellationToken))
        {
            await data.InsertAsync(new Order(Guid.NewGuid(), "rollback@example.com", 5m, false, DateTimeOffset.UtcNow, new OrderMetadata("ops", 1)), TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await data.Query<Order>().CountAsync(TestContext.Current.CancellationToken));

        await using (var transaction = await data.BeginScopeAsync(TestContext.Current.CancellationToken))
        {
            await data.InsertAsync(new Order(Guid.NewGuid(), "commit@example.com", 7m, false, DateTimeOffset.UtcNow, new OrderMetadata("ops", 2)), TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, await data.Query<Order>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Dispatcher_executes_reusable_queries_and_mutations_against_database()
    {
        await ResetTablesAsync(TestContext.Current.CancellationToken);
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var data = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDataDispatcher>();

        var orderId = Guid.NewGuid();
        await data.InsertAsync(new Order(orderId, "alice@example.com", 12.5m, false, DateTimeOffset.UtcNow, new OrderMetadata("web", 1)), TestContext.Current.CancellationToken);

        var unpaidCount = await dispatcher.QueryAsync(new CountOrdersQuery(IsPaid: false), TestContext.Current.CancellationToken);
        var changed = await dispatcher.MutateAsync(new MarkOrderPaidMutation(orderId), TestContext.Current.CancellationToken);

        Assert.Equal(1, unpaidCount);
        Assert.Equal(1, changed);
        Assert.Equal(1, await data.Query<Order>().Where(order => order.IsPaid == true).CountAsync(TestContext.Current.CancellationToken));
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddData(data =>
        {
            data.UsePostgres(options =>
            {
                options.ConnectionString = container.GetConnectionString();
                options.DefaultSchema = "public";
            });
        });

        return services.BuildServiceProvider();
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists public.orders(
                id uuid not null primary key,
                customer_email character varying(200) not null,
                total numeric(18, 2) not null,
                is_paid boolean not null,
                created_at timestamp with time zone not null,
                metadata text not null
            );

            create table if not exists public.customers(
                id uuid not null primary key,
                email character varying(200) not null
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ResetTablesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from public.orders;
            delete from public.customers;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal sealed record Order(Guid Id, string CustomerEmail, decimal Total, bool IsPaid, DateTimeOffset CreatedAt, OrderMetadata Metadata);

    internal sealed record OrderMetadata(string Channel, int Priority);

    internal sealed class OrderMap : EntityMap<Order>
    {
        protected override void Configure(EntityMapBuilder<Order> builder)
        {
            builder.ToTable("public", "orders");
            builder.Property(order => order.Id).HasColumnName("id").IsKeyPart();
            builder.Property(order => order.CustomerEmail).HasColumnName("customer_email").HasStringType(200).IsRequired();
            builder.Property(order => order.Total).HasColumnName("total").HasDecimalType(18, 2);
            builder.Property(order => order.IsPaid).HasColumnName("is_paid");
            builder.Property(order => order.CreatedAt).HasColumnName("created_at");
            builder.Property(order => order.Metadata).HasColumnName("metadata").HasJsonConversion().IsRequired();
        }
    }

    internal sealed record Customer(Guid Id, string Email);

    internal sealed class CustomerMap : EntityMap<Customer>
    {
        protected override void Configure(EntityMapBuilder<Customer> builder)
        {
            builder.ToTable("public", "customers");
            builder.Property(customer => customer.Id).HasColumnName("id").IsKeyPart();
            builder.Property(customer => customer.Email).HasColumnName("email").HasStringType(200).IsRequired();
        }
    }

    private sealed record OrderSummary(Guid Id, string CustomerEmail);

    private sealed record JoinedOrderSummary(string CustomerEmail, string Email);

    internal sealed record CountOrdersQuery(bool IsPaid) : IDataQuery<int>;

    internal sealed class CountOrdersQueryHandler(IDataContext dataContext) : IDataQueryHandler<CountOrdersQuery, int>
    {
        public ValueTask<int> HandleAsync(CountOrdersQuery query, CancellationToken cancellationToken = default) =>
            dataContext.Query<Order>().Where(order => order.IsPaid == query.IsPaid).CountAsync(cancellationToken);
    }

    internal sealed record MarkOrderPaidMutation(Guid OrderId) : IDataMutation<int>;

    internal sealed class MarkOrderPaidMutationHandler(IDataContext dataContext) : IDataMutationHandler<MarkOrderPaidMutation, int>
    {
        public ValueTask<int> HandleAsync(MarkOrderPaidMutation mutation, CancellationToken cancellationToken = default) =>
            dataContext.Update<Order>().Where(order => order.Id == mutation.OrderId).Set(order => order.IsPaid, true).ExecuteAsync(cancellationToken);
    }
}
