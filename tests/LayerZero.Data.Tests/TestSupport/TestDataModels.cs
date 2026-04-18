namespace LayerZero.Data.Tests;

internal sealed record Order(
    Guid Id,
    string CustomerEmail,
    decimal Total,
    bool IsPaid,
    DateTimeOffset CreatedAt,
    OrderMetadata Metadata);

internal sealed record OrderMetadata(string Channel, int Priority);

internal sealed record OrderSummary(Guid Id, string CustomerEmail);

internal sealed record JoinedOrderSummary(string CustomerEmail, string Email);

internal sealed class OrderMap : EntityMap<Order>
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

internal sealed record Customer(Guid Id, string Email);

internal sealed class CustomerMap : EntityMap<Customer>
{
    protected override void Configure(EntityMapBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.Property(customer => customer.Id).HasColumnName("id").IsKeyPart();
        builder.Property(customer => customer.Email).HasColumnName("email").HasStringType(200).IsRequired();
    }
}

internal sealed record ConventionCustomer(Guid Id, string CustomerEmail);

internal sealed class ConventionCustomerMap : EntityMap<ConventionCustomer>
{
    protected override void Configure(EntityMapBuilder<ConventionCustomer> builder)
    {
        builder.Property(customer => customer.Id).IsKeyPart();
        builder.Property(customer => customer.CustomerEmail).HasStringType(200).IsRequired();
    }
}

internal sealed record OverrideCustomer(Guid Id, string CustomerEmail);

internal sealed class OverrideCustomerMap : EntityMap<OverrideCustomer>
{
    protected override void Configure(EntityMapBuilder<OverrideCustomer> builder)
    {
        builder.ToTable("crm", "CustomerRecords");
        builder.Property(customer => customer.Id).HasColumnName("CustomerId").IsKeyPart();
        builder.Property(customer => customer.CustomerEmail).HasColumnName("EmailAddress").HasStringType(256).IsRequired();
    }
}

internal sealed record CurrentAssemblyGreetingQuery(string Name) : IDataQuery<string>;

internal sealed class CurrentAssemblyGreetingQueryHandler : IDataQueryHandler<CurrentAssemblyGreetingQuery, string>
{
    public ValueTask<string> HandleAsync(CurrentAssemblyGreetingQuery query, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult($"Hello {query.Name}");
}

internal sealed record CurrentAssemblyGreetingMutation(string Name) : IDataMutation<string>;

internal sealed class CurrentAssemblyGreetingMutationHandler : IDataMutationHandler<CurrentAssemblyGreetingMutation, string>
{
    public ValueTask<string> HandleAsync(CurrentAssemblyGreetingMutation mutation, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult($"Hello {mutation.Name}!");
}
