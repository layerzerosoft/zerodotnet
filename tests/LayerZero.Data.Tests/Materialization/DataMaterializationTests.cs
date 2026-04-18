using System.Data;
using LayerZero.Data.Internal.Materialization;
using Microsoft.Extensions.DependencyInjection;

namespace LayerZero.Data.Tests.Materialization;

public sealed class DataMaterializationTests
{
    [Fact]
    public async Task Materializer_source_uses_constructor_binding_and_json_converter()
    {
        await using var provider = TestDataServices.BuildProvider();
        var materializers = new DataMaterializerSource(provider.GetRequiredService<LayerZero.Data.Internal.Registration.IEntityMapRegistry>());

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
    public async Task Materializer_source_handles_join_rows()
    {
        await using var provider = TestDataServices.BuildProvider();
        var materializers = new DataMaterializerSource(provider.GetRequiredService<LayerZero.Data.Internal.Registration.IEntityMapRegistry>());

        var table = new DataTable();
        table.Columns.Add("l__id", typeof(Guid));
        table.Columns.Add("l__customer_email", typeof(string));
        table.Columns.Add("l__total", typeof(decimal));
        table.Columns.Add("l__is_paid", typeof(bool));
        table.Columns.Add("l__created_at", typeof(DateTimeOffset));
        table.Columns.Add("l__metadata", typeof(string));
        table.Columns.Add("r__id", typeof(Guid));
        table.Columns.Add("r__email", typeof(string));
        table.Rows.Add(
            Guid.NewGuid(),
            "alice@example.com",
            12.5m,
            true,
            DateTimeOffset.UtcNow,
            """{"channel":"priority","priority":7}""",
            Guid.NewGuid(),
            "alice@example.com");

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var materializer = materializers.GetMaterializer<DataJoin<Order, Customer>>(
            ["l__id", "l__customer_email", "l__total", "l__is_paid", "l__created_at", "l__metadata", "r__id", "r__email"]);
        var joined = materializer(reader);

        Assert.Equal("alice@example.com", joined.Left.CustomerEmail);
        Assert.Equal("alice@example.com", joined.Right.Email);
    }
}
