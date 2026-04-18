namespace LayerZero.Data.Tests.Mapping;

public sealed class EntityMappingConventionTests
{
    [Fact]
    public void EntityMap_defaults_table_and_column_names_to_exact_identifiers()
    {
        var table = new ConventionCustomerMap().Table;

        Assert.Equal("ConventionCustomer", table.Name.Name);
        Assert.Equal(["Id", "CustomerEmail"], table.Columns.Select(static column => column.Name).ToArray());
    }

    [Fact]
    public void EntityMap_respects_explicit_table_and_column_overrides()
    {
        var table = new OverrideCustomerMap().Table;

        Assert.Equal("crm", table.Name.Schema);
        Assert.Equal("CustomerRecords", table.Name.Name);
        Assert.Equal(["CustomerId", "EmailAddress"], table.Columns.Select(static column => column.Name).ToArray());
    }

    [Fact]
    public void EntityMapBuilder_reuses_same_property_configuration()
    {
        var builder = new EntityMapBuilder<ConventionCustomer>();

        var first = builder.Property(customer => customer.CustomerEmail);
        var second = builder.Property(customer => customer.CustomerEmail);

        Assert.Same(first, second);
    }

    [Fact]
    public void EntityMapBuilder_rejects_conflicting_property_types()
    {
        var builder = new EntityMapBuilder<ConventionCustomer>();
        _ = builder.Property(customer => customer.CustomerEmail);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Property(customer => (object?)customer.CustomerEmail));

        Assert.Contains("different CLR type", exception.Message, StringComparison.Ordinal);
    }
}
