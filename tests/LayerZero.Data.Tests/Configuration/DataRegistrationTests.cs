using LayerZero.Data.Configuration;
using LayerZero.Data.Internal.Registration;
using LayerZero.Data.SqlServer;
using LayerZero.Data.TestAssembly;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Tests.Configuration;

public sealed class DataRegistrationTests
{
    [Fact]
    public void AddData_requires_exactly_one_provider()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddData(_ => { }));

        Assert.Contains("exactly one provider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddData_rejects_multiple_providers()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddData(data =>
        {
            data.UseSqlServer(options =>
            {
                options.ConnectionString = "Server=(local);Database=fake;User Id=sa;Password=Password1!;";
                options.DefaultSchema = "dbo";
            });
            data.SelectProvider("fake");
        }));

        Assert.Contains("exactly one provider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddData_implicitly_registers_current_assembly_maps_and_handlers()
    {
        await using var provider = TestDataServices.BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var registry = scope.ServiceProvider.GetRequiredService<IEntityMapRegistry>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDataDispatcher>();

        Assert.Equal(typeof(Order), registry.GetTable(typeof(Order)).EntityType);
        Assert.Equal("Hello LayerZero", await dispatcher.QueryAsync(new CurrentAssemblyGreetingQuery("LayerZero"), TestContext.Current.CancellationToken));
        Assert.Equal("Hello LayerZero!", await dispatcher.MutateAsync(new CurrentAssemblyGreetingMutation("LayerZero"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddData_implicitly_registers_maps_and_handlers_from_referenced_assemblies()
    {
        await using var provider = TestDataServices.BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var registry = scope.ServiceProvider.GetRequiredService<IEntityMapRegistry>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDataDispatcher>();

        Assert.Equal(typeof(ReferencedAccount), registry.GetTable(typeof(ReferencedAccount)).EntityType);
        Assert.Equal(3, await dispatcher.QueryAsync(new CountReferencedAccounts(IsActive: true), TestContext.Current.CancellationToken));
        Assert.Equal(
            "alice@example.com:archived",
            await dispatcher.MutateAsync(new ArchiveReferencedAccounts("alice@example.com"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddData_applies_configured_conventions_to_registered_tables()
    {
        await using var provider = TestDataServices.BuildProvider(data =>
            data.Configure(options => options.Conventions.UseSnakeCaseIdentifiers()));
        await using var scope = provider.CreateAsyncScope();

        var registry = scope.ServiceProvider.GetRequiredService<IEntityMapRegistry>();
        var table = registry.GetTable<ConventionCustomer>();

        Assert.Equal("convention_customer", table.Name.Name);
        Assert.Equal(["id", "customer_email"], table.Columns.Select(static column => column.Name).ToArray());
    }
}
