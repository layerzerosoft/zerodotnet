namespace LayerZero.Data.Analyzers.Tests.Diagnostics;

public sealed class DataOperationDiagnosticTests
{
    [Fact]
    public void Reports_duplicate_maps()
    {
        const string source = """
            using LayerZero.Data;
            using System;

            namespace Demo;

            internal sealed record Account(Guid Id);

            internal sealed class AccountMap : EntityMap<Account>
            {
                protected override void Configure(EntityMapBuilder<Account> builder)
                {
                    builder.Property(account => account.Id).IsKeyPart();
                }
            }

            internal sealed class SecondAccountMap : EntityMap<Account>
            {
                protected override void Configure(EntityMapBuilder<Account> builder)
                {
                    builder.Property(account => account.Id).IsKeyPart();
                }
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZDATA002");
    }

    [Fact]
    public void Reports_duplicate_query_handlers()
    {
        const string source = """
            using LayerZero.Data;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo;

            internal sealed record GetAccounts() : IDataQuery<int>;

            internal sealed class FirstHandler : IDataQueryHandler<GetAccounts, int>
            {
                public ValueTask<int> HandleAsync(GetAccounts query, CancellationToken cancellationToken = default) => ValueTask.FromResult(1);
            }

            internal sealed class SecondHandler : IDataQueryHandler<GetAccounts, int>
            {
                public ValueTask<int> HandleAsync(GetAccounts query, CancellationToken cancellationToken = default) => ValueTask.FromResult(2);
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZDATA003");
    }

    [Fact]
    public void Reports_duplicate_mutation_handlers()
    {
        const string source = """
            using LayerZero.Data;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo;

            internal sealed record ArchiveAccounts() : IDataMutation<int>;

            internal sealed class FirstHandler : IDataMutationHandler<ArchiveAccounts, int>
            {
                public ValueTask<int> HandleAsync(ArchiveAccounts mutation, CancellationToken cancellationToken = default) => ValueTask.FromResult(1);
            }

            internal sealed class SecondHandler : IDataMutationHandler<ArchiveAccounts, int>
            {
                public ValueTask<int> HandleAsync(ArchiveAccounts mutation, CancellationToken cancellationToken = default) => ValueTask.FromResult(2);
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZDATA004");
    }

    [Fact]
    public void Reports_non_instantiable_registered_types()
    {
        const string source = """
            using LayerZero.Data;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Demo;

            internal sealed record Account(Guid Id);

            internal sealed class AccountMap : EntityMap<Account>
            {
                private AccountMap() { }

                protected override void Configure(EntityMapBuilder<Account> builder)
                {
                    builder.Property(account => account.Id).IsKeyPart();
                }
            }

            internal sealed record GetAccounts() : IDataQuery<int>;

            internal sealed class GetAccountsHandler : IDataQueryHandler<GetAccounts, int>
            {
                private GetAccountsHandler() { }

                public ValueTask<int> HandleAsync(GetAccounts query, CancellationToken cancellationToken = default) => ValueTask.FromResult(42);
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZDATA005");
    }

    [Fact]
    public void Reports_generated_type_collisions()
    {
        const string source = """
            namespace LayerZero.Data.Generated;

            internal sealed class LayerZero_Data_Analyzers_Tests_InputDataAssemblyRegistrar
            {
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "LZDATA001");
    }
}
