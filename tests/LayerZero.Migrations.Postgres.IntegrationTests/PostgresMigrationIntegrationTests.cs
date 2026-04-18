using LayerZero.Data;
using LayerZero.Data.Postgres.Configuration;
using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using LayerZero.Migrations.Postgres.Configuration;
using LayerZero.Migrations.Postgres.Internal;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LayerZero.Migrations.Postgres.IntegrationTests;

public sealed class PostgresMigrationIntegrationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Apply_executes_pending_migrations_and_selected_seed_profiles()
    {
        var connectionString = await fixture.CreateDatabaseConnectionStringAsync(TestContext.Current.CancellationToken);
        var runtime = CreateRuntime(connectionString, CreateReferenceDataRegistry());

        var result = await runtime.ApplyAsync(
            new MigrationApplyOptions
            {
                Profiles = ["dev"],
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(2, await ExecuteScalarAsync<int>(connectionString, "select count(*) from public.roles;", TestContext.Current.CancellationToken));
        Assert.Equal(3, await ExecuteScalarAsync<int>(connectionString, "select count(*) from public.\"__LayerZeroMigrationsHistory\";", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Apply_is_idempotent_when_re_run()
    {
        var connectionString = await fixture.CreateDatabaseConnectionStringAsync(TestContext.Current.CancellationToken);
        var runtime = CreateRuntime(connectionString, CreateReferenceDataRegistry());

        var first = await runtime.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);
        var second = await runtime.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, first.Items.Count);
        Assert.Empty(second.Items);
        Assert.Equal(2, await ExecuteScalarAsync<int>(connectionString, "select count(*) from public.\"__LayerZeroMigrationsHistory\";", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Validate_reports_checksum_drift_for_applied_history()
    {
        var connectionString = await fixture.CreateDatabaseConnectionStringAsync(TestContext.Current.CancellationToken);
        var original = CreateRuntime(connectionString, CreateLedgerRegistry(withAdditionalColumn: false));
        await original.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        var drifted = CreateRuntime(connectionString, CreateLedgerRegistry(withAdditionalColumn: true));
        var validation = await drifted.ValidateAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Code == "layerzero.migrations.checksum_mismatch");
    }

    [Fact]
    public async Task Baseline_marks_existing_schema_without_running_the_migration_body()
    {
        var connectionString = await fixture.CreateDatabaseConnectionStringAsync(TestContext.Current.CancellationToken);
        await ExecuteNonQueryAsync(
            connectionString,
            """
            create table public.ledger(
                id uuid not null primary key,
                reference character varying(128) not null
            );
            """,
            TestContext.Current.CancellationToken);

        var runtime = CreateRuntime(connectionString, CreateLedgerRegistry(withAdditionalColumn: false));
        var baseline = await runtime.BaselineAsync(cancellationToken: TestContext.Current.CancellationToken);
        var apply = await runtime.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(baseline.Items);
        Assert.Empty(apply.Items);
        Assert.Equal(1, await ExecuteScalarAsync<int>(connectionString, "select count(*) from public.\"__LayerZeroMigrationsHistory\";", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_runners_serialize_through_postgres_advisory_locks()
    {
        var connectionString = await fixture.CreateDatabaseConnectionStringAsync(TestContext.Current.CancellationToken);
        var first = CreateRuntime(connectionString, CreateDelayedRegistry());
        var second = CreateRuntime(connectionString, CreateDelayedRegistry());

        await Task.WhenAll(
            first.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask(),
            second.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(1, await ExecuteScalarAsync<int>(connectionString, "select count(*) from public.\"__LayerZeroMigrationsHistory\";", TestContext.Current.CancellationToken));
        Assert.Equal(1, await ExecuteScalarAsync<int>(connectionString, "select count(*) from public.delayed_artifacts;", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Generate_script_does_not_wrap_non_transactional_migrations_in_transactions()
    {
        var adapter = new PostgresMigrationDatabaseAdapter(
            new TestDatabaseConnectionFactory("Host=localhost;Database=postgres;Username=postgres;Password=postgres"),
            Options.Create(new PostgresDataOptions
            {
                ConnectionString = "Host=localhost;Database=postgres;Username=postgres;Password=postgres",
            }),
            Options.Create(new PostgresMigrationsOptions()));
        var model = new MigrationModelCompiler().Compile(CreateDelayedRegistry());

        var script = adapter.GenerateScript(
            MigrationExecutionMode.Apply,
            new MigrationsOptions
            {
                HistoryTableSchema = "public",
                Executor = "tests",
            },
            "tests",
            model.Migrations);

        Assert.Contains("pg_sleep", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("begin;", script, StringComparison.OrdinalIgnoreCase);
    }

    private static MigrationRuntime CreateRuntime(string connectionString, IMigrationCatalog registry)
    {
        return new MigrationRuntime(
            registry,
            new PostgresMigrationDatabaseAdapter(
                new TestDatabaseConnectionFactory(connectionString),
                Options.Create(new PostgresDataOptions
                {
                    ConnectionString = connectionString,
                }),
                Options.Create(new PostgresMigrationsOptions
                {
                    LockTimeout = TimeSpan.FromSeconds(30),
                })),
            new MigrationModelCompiler(),
            Options.Create(new MigrationsOptions
            {
                HistoryTableSchema = "public",
                Executor = "integration-tests",
            }));
    }

    private static IMigrationCatalog CreateReferenceDataRegistry()
    {
        return new DescriptorRegistry(
            migrations:
            [
                new MigrationDescriptor("20260414120000", "Create roles table", typeof(CreateRolesTableMigration), static () => new CreateRolesTableMigration()),
            ],
            seeds:
            [
                new SeedDescriptor("20260414121000", "Baseline roles", SeedProfiles.Baseline, typeof(BaselineRolesSeed), static () => new BaselineRolesSeed()),
                new SeedDescriptor("20260414122000", "Developer roles", "dev", typeof(DeveloperRolesSeed), static () => new DeveloperRolesSeed()),
            ]);
    }

    private static IMigrationCatalog CreateLedgerRegistry(bool withAdditionalColumn)
    {
        return new DescriptorRegistry(
            migrations:
            [
                new MigrationDescriptor(
                    "20260414123000",
                    "Create ledger table",
                    typeof(CreateLedgerMigration),
                    () => new CreateLedgerMigration(withAdditionalColumn)),
            ],
            seeds: []);
    }

    private static IMigrationCatalog CreateDelayedRegistry()
    {
        return new DescriptorRegistry(
            migrations:
            [
                new MigrationDescriptor(
                    "20260414124000",
                    "Create delayed artifact table",
                    typeof(CreateDelayedArtifactMigration),
                    static () => new CreateDelayedArtifactMigration()),
            ],
            seeds: []);
    }

    private static async Task ExecuteNonQueryAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    private sealed class DescriptorRegistry(
        IReadOnlyList<MigrationDescriptor> migrations,
        IReadOnlyList<SeedDescriptor> seeds) : IMigrationCatalog
    {
        public IReadOnlyList<MigrationDescriptor> Migrations { get; } = migrations;

        public IReadOnlyList<SeedDescriptor> Seeds { get; } = seeds;
    }

    private sealed class RoleMap : EntityMap<Role>
    {
        protected override void Configure(EntityMapBuilder<Role> builder)
        {
            builder.ToTable("roles");
            builder.Property(role => role.Id).IsKeyPart();
            builder.Property(role => role.Name).HasStringType(128).IsRequired();
            builder.HasIndex("IX_roles_name", isUnique: true, role => role.Name);
        }
    }

    private sealed record Role(int Id, string Name);

    private sealed class CreateRolesTableMigration : Migration
    {
        public override void Build(MigrationBuilder builder)
        {
            var map = new RoleMap().Table;
            builder.CreateTable(map);
            builder.CreateIndex(map, map.Indexes[0]);
        }
    }

    private sealed class BaselineRolesSeed : Seed
    {
        public override void Build(SeedBuilder builder)
        {
            var table = new RoleMap().Table;
            builder.InsertData(table, rows =>
            {
                rows.Row(row => row
                    .Set((EntityColumn<Role, int>)table.PrimaryKeyColumns[0], 1)
                    .Set((EntityColumn<Role, string>)table.Columns.Single(column => column.Name == "Name"), "admin"));
            });
        }
    }

    private sealed class DeveloperRolesSeed : Seed
    {
        public override void Build(SeedBuilder builder)
        {
            var table = new RoleMap().Table;
            var idColumn = (EntityColumn<Role, int>)table.PrimaryKeyColumns[0];
            var nameColumn = (EntityColumn<Role, string>)table.Columns.Single(column => column.Name == "Name");
            builder.InsertData(table, rows =>
            {
                rows.Row(row => row.Set(idColumn, 2).Set(nameColumn, "developer"));
            });
        }
    }

    private sealed class CreateLedgerMigration(bool withAdditionalColumn) : Migration
    {
        public override void Build(MigrationBuilder builder)
        {
            builder.CreateTable("ledger", table =>
            {
                table.Column("id").AsGuid().NotNull();
                table.Column("reference").AsString(128).NotNull();
                if (withAdditionalColumn)
                {
                    table.Column("category").AsString(64).Nullable();
                }

                table.PrimaryKey("id");
            });
        }
    }

    private sealed class CreateDelayedArtifactMigration : Migration
    {
        public override MigrationTransactionMode TransactionMode => MigrationTransactionMode.NonTransactional;

        public override void Build(MigrationBuilder builder)
        {
            builder.Sql("select pg_sleep(1);");
            builder.CreateTable("delayed_artifacts", table =>
            {
                table.Column("id").AsInt32().NotNull();
                table.PrimaryKey("id");
            });
            builder.InsertData("delayed_artifacts", rows =>
            {
                rows.Row(row => row.Set("id", 1));
            });
        }
    }

    private sealed class TestDatabaseConnectionFactory(string connectionString) : IDatabaseConnectionFactory
    {
        public string ProviderName => "postgres";

        public async ValueTask<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16.4").Build();

    public async ValueTask InitializeAsync()
    {
        await Container.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await Container.DisposeAsync();

    public async Task<string> CreateDatabaseConnectionStringAsync(CancellationToken cancellationToken)
    {
        var databaseName = $"lz_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(Container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""create database "{databaseName}";""";
        await command.ExecuteNonQueryAsync(cancellationToken);

        var builder = new NpgsqlConnectionStringBuilder(Container.GetConnectionString())
        {
            Database = databaseName,
        };

        return builder.ConnectionString;
    }
}
