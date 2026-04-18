using System.Linq.Expressions;
using LayerZero.Data.Internal.Execution;
using LayerZero.Data.Internal.Registration;
using LayerZero.Data.Internal.Sql;
using LayerZero.Data.Internal.Translation;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Tests.Translation;

public sealed class DataTranslationTests
{
    [Fact]
    public async Task Query_translation_renders_expected_sql()
    {
        var model = DataQueryModel.Create<Order>()
            .AddFilter((Expression<Func<Order, bool>>)(order => order.CustomerEmail == "alice@example.com" && order.Total > 10m))
            .AddOrdering((Expression<Func<Order, DateTimeOffset>>)(order => order.CreatedAt), descending: true)
            .WithTake(5);

        Expression<Func<Order, OrderSummary>> projection = order => new OrderSummary(order.Id, order.CustomerEmail);

        await using var provider = TestDataServices.BuildProvider();
        var registry = provider.GetRequiredService<IEntityMapRegistry>();
        var template = DataCommandTranslation.CreateReaderTemplate<OrderSummary>(model, projection, registry);
        var compiled = TestDataServices.CreateDialect().CompileReader(template, DataReadMode.List);

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

        var parameters = DataCommandTranslation.CollectInsertParameterValues(order, new OrderMap().Table);

        Assert.Contains(parameters, value => value is string text && text.Contains("\"priority\":3", StringComparison.Ordinal));
    }

    [Fact]
    public void Reader_cache_keys_are_stable_for_different_parameter_values()
    {
        var firstMinimum = 10m;
        var secondMinimum = 20m;

        var firstModel = DataQueryModel.Create<Order>()
            .AddFilter((Expression<Func<Order, bool>>)(order => order.Total > firstMinimum));
        var secondModel = DataQueryModel.Create<Order>()
            .AddFilter((Expression<Func<Order, bool>>)(order => order.Total > secondMinimum));

        var first = DataCommandTranslation.CreateReaderCacheKey(firstModel, projection: null, DataReadMode.List, typeof(Order));
        var second = DataCommandTranslation.CreateReaderCacheKey(secondModel, projection: null, DataReadMode.List, typeof(Order));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Update_and_delete_cache_keys_are_stable_for_different_parameter_values()
    {
        var firstUpdate = DataUpdateModel.Create<Order>()
            .AddAssignment((Expression<Func<Order, bool>>)(order => order.IsPaid), true, typeof(bool))
            .AddFilter((Expression<Func<Order, bool>>)(order => order.CustomerEmail == "alice@example.com"));
        var secondUpdate = DataUpdateModel.Create<Order>()
            .AddAssignment((Expression<Func<Order, bool>>)(order => order.IsPaid), false, typeof(bool))
            .AddFilter((Expression<Func<Order, bool>>)(order => order.CustomerEmail == "bob@example.com"));

        var firstDelete = DataDeleteModel.Create<Order>()
            .AddFilter((Expression<Func<Order, bool>>)(order => order.CustomerEmail == "alice@example.com"));
        var secondDelete = DataDeleteModel.Create<Order>()
            .AddFilter((Expression<Func<Order, bool>>)(order => order.CustomerEmail == "bob@example.com"));

        Assert.Equal(DataCommandTranslation.CreateUpdateCacheKey(firstUpdate), DataCommandTranslation.CreateUpdateCacheKey(secondUpdate));
        Assert.Equal(DataCommandTranslation.CreateDeleteCacheKey(firstDelete), DataCommandTranslation.CreateDeleteCacheKey(secondDelete));
    }
}
