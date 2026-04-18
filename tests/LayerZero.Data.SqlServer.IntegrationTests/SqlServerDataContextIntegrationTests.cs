using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace LayerZero.Data.SqlServer.IntegrationTests;

public sealed class SqlServerDataContextIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

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

        var alice = new Order(Guid.NewGuid(), "alice@example.com", 12.5m, false, DateTimeOffset.UtcNow);
        var bob = new Order(Guid.NewGuid(), "bob@example.com", 15m, false, DateTimeOffset.UtcNow.AddMinutes(1));

        await data.InsertAsync(alice, TestContext.Current.CancellationToken);
        await data.InsertAsync(bob, TestContext.Current.CancellationToken);

        var summaries = await data.Query<Order>()
            .Where(order => order.IsPaid == false)
            .OrderBy(order => order.CustomerEmail)
            .Select(order => new OrderSummary(order.Id, order.CustomerEmail))
            .ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, summaries.Count);
        Assert.Equal(27.5m, await data.Query<Order>().SumAsync(order => order.Total, TestContext.Current.CancellationToken));

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
    public async Task Direct_context_can_join_and_project()
    {
        await ResetTablesAsync(TestContext.Current.CancellationToken);
        await SeedCustomersAsync(TestContext.Current.CancellationToken);

        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var data = scope.ServiceProvider.GetRequiredService<IDataContext>();

        await data.InsertAsync(new Customer(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "alice@example.com"), TestContext.Current.CancellationToken);
        await data.InsertAsync(new Order(Guid.NewGuid(), "alice@example.com", 12.5m, true, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        var rows = await data.Query<Order>()
            .Join<Customer, string>(order => order.CustomerEmail, customer => customer.Email)
            .Where(joined => joined.Left.IsPaid == true)
            .Select(joined => new JoinedOrderSummary(joined.Left.CustomerEmail, joined.Right.Email))
            .ListAsync(TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Equal("alice@example.com", rows[0].CustomerEmail);
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
            await data.InsertAsync(new Order(Guid.NewGuid(), "rollback@example.com", 5m, false, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await data.Query<Order>().CountAsync(TestContext.Current.CancellationToken));

        await using (var transaction = await data.BeginScopeAsync(TestContext.Current.CancellationToken))
        {
            await data.InsertAsync(new Order(Guid.NewGuid(), "commit@example.com", 7m, false, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
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
        await data.InsertAsync(new Order(orderId, "alice@example.com", 12.5m, false, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

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
            data.UseSqlServer(options =>
            {
                options.ConnectionString = container.GetConnectionString();
                options.DefaultSchema = "dbo";
            });
        });

        return services.BuildServiceProvider();
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            if object_id('dbo.orders', 'U') is null
            begin
                create table dbo.orders(
                    id uniqueidentifier not null primary key,
                    customer_email nvarchar(200) not null,
                    total decimal(18, 2) not null,
                    is_paid bit not null,
                    created_at datetimeoffset not null
                );
            end;

            if object_id('dbo.customers', 'U') is null
            begin
                create table dbo.customers(
                    id uniqueidentifier not null primary key,
                    email nvarchar(200) not null
                );
            end;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ResetTablesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from dbo.orders;
            delete from dbo.customers;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Task SeedCustomersAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal sealed record Order(Guid Id, string CustomerEmail, decimal Total, bool IsPaid, DateTimeOffset CreatedAt);

    internal sealed class OrderMap : EntityMap<Order>
    {
        protected override void Configure(EntityMapBuilder<Order> builder)
        {
            builder.ToTable("dbo", "orders");
            builder.Property(order => order.Id).HasColumnName("id").IsKeyPart();
            builder.Property(order => order.CustomerEmail).HasColumnName("customer_email").HasStringType(200).IsRequired();
            builder.Property(order => order.Total).HasColumnName("total").HasDecimalType(18, 2);
            builder.Property(order => order.IsPaid).HasColumnName("is_paid");
            builder.Property(order => order.CreatedAt).HasColumnName("created_at");
        }
    }

    internal sealed record Customer(Guid Id, string Email);

    internal sealed class CustomerMap : EntityMap<Customer>
    {
        protected override void Configure(EntityMapBuilder<Customer> builder)
        {
            builder.ToTable("dbo", "customers");
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
