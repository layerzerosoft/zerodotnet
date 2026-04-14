using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using Microsoft.Extensions.Options;

namespace LayerZero.Migrations.Tests;

public sealed class MigrationRuntimeTests
{
    [Fact]
    public void Compiler_produces_stable_checksums_for_the_same_registry()
    {
        var compiler = new MigrationModelCompiler();
        var registry = new TestRegistry(
            migrations:
            [
                new MigrationDescriptor(
                    "20260414120000",
                    "Create accounts",
                    typeof(CreateAccountsMigration),
                    MigrationTransactionMode.Transactional,
                    static () => new CreateAccountsMigration()),
            ],
            seeds:
            [
                new SeedDescriptor(
                    "20260414121000",
                    "Baseline roles",
                    SeedProfiles.Baseline,
                    typeof(BaselineRolesSeed),
                    static () => new BaselineRolesSeed()),
            ]);

        var first = compiler.Compile(registry);
        var second = compiler.Compile(registry);

        Assert.Equal(first.Migrations[0].Checksum, second.Migrations[0].Checksum);
        Assert.Equal(first.Seeds[0].Checksum, second.Seeds[0].Checksum);
    }

    [Fact]
    public async Task Validate_reports_out_of_order_migrations()
    {
        var adapter = new FakeMigrationDatabaseAdapter
        {
            State = new MigrationDatabaseSnapshot(
                historyExists: true,
                hasUserObjects: true,
                [
                    new AppliedArtifactRecord(
                        MigrationArtifactKind.Migration,
                        "20260414130000",
                        string.Empty,
                        "Create ledger",
                        "ABC",
                        DateTimeOffset.UtcNow,
                        "tests"),
                ]),
        };

        var runtime = CreateRuntime(
            adapter,
            new TestRegistry(
                migrations:
                [
                    new MigrationDescriptor("20260414120000", "Create accounts", typeof(CreateAccountsMigration), MigrationTransactionMode.Transactional, static () => new CreateAccountsMigration()),
                    new MigrationDescriptor("20260414130000", "Create ledger", typeof(CreateLedgerMigration), MigrationTransactionMode.Transactional, static () => new CreateLedgerMigration()),
                ],
                seeds: []));

        var result = await runtime.ValidateAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "layerzero.migrations.out_of_order");
    }

    [Fact]
    public async Task Info_selects_baseline_seeds_by_default_and_opt_in_profiles_explicitly()
    {
        var runtime = CreateRuntime(
            new FakeMigrationDatabaseAdapter(),
            new TestRegistry(
                migrations: [],
                seeds:
                [
                    new SeedDescriptor("20260414121000", "Baseline roles", SeedProfiles.Baseline, typeof(BaselineRolesSeed), static () => new BaselineRolesSeed()),
                    new SeedDescriptor("20260414122000", "Developer roles", "dev", typeof(DeveloperRolesSeed), static () => new DeveloperRolesSeed()),
                ]));

        var result = await runtime.InfoAsync(
            new MigrationInfoOptions
            {
                Profiles = ["dev"],
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["baseline", "dev"], result.SelectedProfiles);
        Assert.Contains(result.Items, item => item.Kind == MigrationArtifactKind.Seed && item.Profile == SeedProfiles.Baseline);
        Assert.Contains(result.Items, item => item.Kind == MigrationArtifactKind.Seed && item.Profile == "dev");
    }

    [Fact]
    public async Task Apply_refuses_to_take_over_non_empty_databases_without_history()
    {
        var adapter = new FakeMigrationDatabaseAdapter
        {
            State = new MigrationDatabaseSnapshot(
                historyExists: false,
                hasUserObjects: true,
                []),
        };

        var runtime = CreateRuntime(
            adapter,
            new TestRegistry(
                migrations:
                [
                    new MigrationDescriptor("20260414120000", "Create accounts", typeof(CreateAccountsMigration), MigrationTransactionMode.Transactional, static () => new CreateAccountsMigration()),
                ],
                seeds: []));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runtime.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());

        Assert.Contains("baseline", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MigrationRuntime CreateRuntime(FakeMigrationDatabaseAdapter adapter, IMigrationRegistry registry)
    {
        return new MigrationRuntime(
            registry,
            adapter,
            new MigrationModelCompiler(),
            Options.Create(new MigrationsOptions()));
    }

    private sealed class TestRegistry(
        IReadOnlyList<MigrationDescriptor> migrations,
        IReadOnlyList<SeedDescriptor> seeds) : IMigrationRegistry
    {
        public IReadOnlyList<MigrationDescriptor> Migrations { get; } = migrations;

        public IReadOnlyList<SeedDescriptor> Seeds { get; } = seeds;
    }

    private sealed class FakeMigrationDatabaseAdapter : IMigrationDatabaseAdapter
    {
        public MigrationDatabaseSnapshot State { get; set; } = new(historyExists: false, hasUserObjects: false, []);

        public List<(MigrationExecutionMode Mode, IReadOnlyList<CompiledArtifact> Artifacts)> Executions { get; } = [];

        public ValueTask<MigrationDatabaseSnapshot> ReadStateAsync(MigrationsOptions options, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(State);
        }

        public ValueTask EnsureHistoryStoreAsync(MigrationsOptions options, CancellationToken cancellationToken)
        {
            State = new MigrationDatabaseSnapshot(historyExists: true, State.HasUserObjects, State.AppliedArtifacts);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IAsyncDisposable> AcquireLockAsync(MigrationsOptions options, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IAsyncDisposable>(new NoOpAsyncDisposable());
        }

        public string GenerateScript(
            MigrationExecutionMode mode,
            MigrationsOptions options,
            string executor,
            IReadOnlyList<CompiledArtifact> artifacts)
        {
            return string.Join(Environment.NewLine, artifacts.Select(static artifact => artifact.Id));
        }

        public ValueTask ExecuteAsync(
            MigrationExecutionMode mode,
            MigrationsOptions options,
            string executor,
            IReadOnlyList<CompiledArtifact> artifacts,
            CancellationToken cancellationToken)
        {
            Executions.Add((mode, artifacts));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CreateAccountsMigration : Migration
    {
        public CreateAccountsMigration()
            : base("20260414120000", "Create accounts")
        {
        }

        public override void Build(MigrationBuilder builder)
        {
            builder.CreateTable("accounts", table =>
            {
                table.Column("id").AsGuid().NotNull();
                table.Column("email").AsString(256).NotNull();
                table.PrimaryKey("id");
            });
        }
    }

    private sealed class CreateLedgerMigration : Migration
    {
        public CreateLedgerMigration()
            : base("20260414130000", "Create ledger")
        {
        }

        public override void Build(MigrationBuilder builder)
        {
            builder.CreateTable("ledger", table =>
            {
                table.Column("id").AsGuid().NotNull();
                table.Column("reference").AsString(128).NotNull();
                table.PrimaryKey("id");
            });
        }
    }

    private sealed class BaselineRolesSeed : Seed
    {
        public BaselineRolesSeed()
            : base("20260414121000", "Baseline roles")
        {
        }

        public override void Build(SeedBuilder builder)
        {
            builder.InsertData("roles", rows =>
            {
                rows.Row(row => row.Set("id", 1).Set("name", "admin"));
            });
        }
    }

    private sealed class DeveloperRolesSeed : Seed
    {
        public DeveloperRolesSeed()
            : base("20260414122000", "Developer roles", "dev")
        {
        }

        public override void Build(SeedBuilder builder)
        {
            builder.InsertData("roles", rows =>
            {
                rows.Row(row => row.Set("id", 2).Set("name", "developer"));
            });
        }
    }
}
