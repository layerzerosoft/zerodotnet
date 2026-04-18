namespace LayerZero.Data.TestAssembly;

public sealed record ReferencedAccount(Guid Id, string Email, bool IsActive);

public sealed class ReferencedAccountMap : EntityMap<ReferencedAccount>
{
    protected override void Configure(EntityMapBuilder<ReferencedAccount> builder)
    {
        builder.Property(account => account.Id).IsKeyPart();
        builder.Property(account => account.Email).HasStringType(256).IsRequired();
        builder.Property(account => account.IsActive);
    }
}

public sealed record CountReferencedAccounts(bool IsActive) : IDataQuery<int>;

public sealed class CountReferencedAccountsHandler : IDataQueryHandler<CountReferencedAccounts, int>
{
    public ValueTask<int> HandleAsync(CountReferencedAccounts query, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(query.IsActive ? 3 : 0);
}

public sealed record ArchiveReferencedAccounts(string Email) : IDataMutation<string>;

public sealed class ArchiveReferencedAccountsHandler : IDataMutationHandler<ArchiveReferencedAccounts, string>
{
    public ValueTask<string> HandleAsync(ArchiveReferencedAccounts mutation, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult($"{mutation.Email}:archived");
}
