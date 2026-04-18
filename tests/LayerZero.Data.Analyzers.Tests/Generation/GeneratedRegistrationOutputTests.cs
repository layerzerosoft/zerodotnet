namespace LayerZero.Data.Analyzers.Tests.Generation;

public sealed class GeneratedRegistrationOutputTests
{
    [Fact]
    public void Generates_registrar_attribute_and_module_initializer_for_maps_and_handlers()
    {
        const string source = """
            using LayerZero.Data;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo;

            internal sealed record Account(Guid Id, string Email);

            internal sealed class AccountMap : EntityMap<Account>
            {
                protected override void Configure(EntityMapBuilder<Account> builder)
                {
                    builder.Property(account => account.Id).IsKeyPart();
                    builder.Property(account => account.Email).HasStringType(200).IsRequired();
                }
            }

            internal sealed record GetAccounts() : IDataQuery<int>;

            internal sealed class GetAccountsHandler : IDataQueryHandler<GetAccounts, int>
            {
                public ValueTask<int> HandleAsync(GetAccounts query, CancellationToken cancellationToken = default) => ValueTask.FromResult(42);
            }

            internal sealed record ArchiveAccounts() : IDataMutation<int>;

            internal sealed class ArchiveAccountsHandler : IDataMutationHandler<ArchiveAccounts, int>
            {
                public ValueTask<int> HandleAsync(ArchiveAccounts mutation, CancellationToken cancellationToken = default) => ValueTask.FromResult(1);
            }
            """;

        var result = GeneratorTestHarness.Run(source);
        var output = string.Join(Environment.NewLine, result.GeneratedSources.Select(static generated => generated.SourceText.ToString()));

        Assert.Empty(result.Diagnostics);
        Assert.Contains("DataAssemblyRegistrarAttribute", output, StringComparison.Ordinal);
        Assert.Contains("IDataAssemblyRegistrar", output, StringComparison.Ordinal);
        Assert.Contains("builder.AddEntityMap<global::Demo.AccountMap>();", output, StringComparison.Ordinal);
        Assert.Contains("builder.AddQueryHandler<global::Demo.GetAccountsHandler, global::Demo.GetAccounts, int>();", output, StringComparison.Ordinal);
        Assert.Contains("builder.AddMutationHandler<global::Demo.ArchiveAccountsHandler, global::Demo.ArchiveAccounts, int>();", output, StringComparison.Ordinal);
        Assert.Contains("DataAssemblyRegistrarCatalog.Register<", output, StringComparison.Ordinal);
    }
}
