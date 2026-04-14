using System.Data.Common;
using LayerZero.Data;
using LayerZero.Data.SqlServer.Configuration;
using LayerZero.Migrations.Configuration;
using LayerZero.Migrations.Internal;
using LayerZero.Migrations.SqlServer.Configuration;
using LayerZero.Migrations.SqlServer.Internal;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;

namespace LayerZero.Migrations.SqlServer.IntegrationTests;

public sealed class SqlServerMigrationIntegrationTests(MsSqlFixture fixture) : IClassFixture<MsSqlFixture>
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
        Assert.Equal(2, await ExecuteScalarAsync<int>(connectionString, "select count(*) from [dbo].[roles];", TestContext.Current.CancellationToken));
        Assert.Equal(3, await ExecuteScalarAsync<int>(connectionString, "select count(*) from [dbo].[__LayerZeroMigrationsHistory];", TestContext.Current.CancellationToken));
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
        Assert.Equal(2, await ExecuteScalarAsync<int>(connectionString, "select count(*) from [dbo].[__LayerZeroMigrationsHistory];", TestContext.Current.CancellationToken));
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
            create table [dbo].[ledger](
                [id] uniqueidentifier not null primary key,
                [reference] nvarchar(128) not null
            );
            """,
            TestContext.Current.CancellationToken);

        var runtime = CreateRuntime(connectionString, CreateLedgerRegistry(withAdditionalColumn: false));
        var baseline = await runtime.BaselineAsync(cancellationToken: TestContext.Current.CancellationToken);
        var apply = await runtime.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(baseline.Items);
        Assert.Empty(apply.Items);
        Assert.Equal(1, await ExecuteScalarAsync<int>(connectionString, "select count(*) from [dbo].[__LayerZeroMigrationsHistory];", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_runners_serialize_through_sql_server_app_locks()
    {
        var connectionString = await fixture.CreateDatabaseConnectionStringAsync(TestContext.Current.CancellationToken);
        var first = CreateRuntime(connectionString, CreateDelayedRegistry());
        var second = CreateRuntime(connectionString, CreateDelayedRegistry());

        await Task.WhenAll(
            first.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask(),
            second.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(1, await ExecuteScalarAsync<int>(connectionString, "select count(*) from [dbo].[__LayerZeroMigrationsHistory];", TestContext.Current.CancellationToken));
        Assert.Equal(1, await ExecuteScalarAsync<int>(connectionString, "select count(*) from [dbo].[delayed_artifacts];", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Generate_script_does_not_wrap_non_transactional_migrations_in_transactions()
    {
        var adapter = new SqlServerMigrationDatabaseAdapter(
            new TestDatabaseConnectionFactory("Server=.;Database=tempdb;User ID=sa;Password=Test12345!"),
            Options.Create(new SqlServerDataOptions
            {
                ConnectionString = "Server=.;Database=tempdb;User ID=sa;Password=Test12345!",
            }),
            Options.Create(new SqlServerMigrationsOptions
            {
            }));
        var model = new MigrationModelCompiler().Compile(CreateDelayedRegistry());

        var script = adapter.GenerateScript(
            MigrationExecutionMode.Apply,
            new MigrationsOptions
            {
                Executor = "tests",
            },
            "tests",
            model.Migrations);

        Assert.Contains("waitfor delay", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("begin transaction;", script, StringComparison.OrdinalIgnoreCase);
    }

    private static MigrationRuntime CreateRuntime(string connectionString, IMigrationCatalog registry)
    {
        return new MigrationRuntime(
            registry,
            new SqlServerMigrationDatabaseAdapter(
                new TestDatabaseConnectionFactory(connectionString),
                Options.Create(new SqlServerDataOptions
                {
                    ConnectionString = connectionString,
                }),
                Options.Create(new SqlServerMigrationsOptions
                {
                    LockTimeout = TimeSpan.FromSeconds(30),
                })),
            new MigrationModelCompiler(),
            Options.Create(new MigrationsOptions
            {
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
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
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
                    .Set((EntityColumn<Role, string>)table.Columns.Single(column => column.Name == "name"), "admin"));
            });
        }
    }

    private sealed class DeveloperRolesSeed : Seed
    {
        public override void Build(SeedBuilder builder)
        {
            var table = new RoleMap().Table;
            var idColumn = (EntityColumn<Role, int>)table.PrimaryKeyColumns[0];
            var nameColumn = (EntityColumn<Role, string>)table.Columns.Single(column => column.Name == "name");
            builder.UpsertData(table, [idColumn], row => row.Set(idColumn, 2).Set(nameColumn, "developer"));
        }
    }

    private sealed class CreateLedgerMigration : Migration
    {
        private readonly bool withAdditionalColumn;

        public CreateLedgerMigration()
            : this(false)
        {
        }

        public CreateLedgerMigration(bool withAdditionalColumn)
        {
            this.withAdditionalColumn = withAdditionalColumn;
        }

        public override void Build(MigrationBuilder builder)
        {
            builder.CreateTable("ledger", table =>
            {
                table.Column("id").AsGuid().NotNull();
                table.Column("reference").AsString(128).NotNull();
                if (withAdditionalColumn)
                {
                    table.Column("description").AsString(256).Nullable();
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
            builder.Sql(
                """
                waitfor delay '00:00:02';
                if object_id(N'[dbo].[delayed_artifacts]', N'U') is null
                    create table [dbo].[delayed_artifacts](
                        [id] int not null primary key
                    );
                if not exists (select 1 from [dbo].[delayed_artifacts] where [id] = 1)
                    insert into [dbo].[delayed_artifacts]([id]) values (1);
                """);
        }
    }

    private sealed class TestDatabaseConnectionFactory(string connectionString) : IDatabaseConnectionFactory
    {
        public string ProviderName => "sqlserver";

        public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}

public sealed class MsSqlFixture : IAsyncLifetime
{
    public MsSqlContainer Container { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await Container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
    }

    public async Task<string> CreateDatabaseConnectionStringAsync(CancellationToken cancellationToken)
    {
        var databaseName = "LZ" + Guid.NewGuid().ToString("N");
        await using var connection = new SqlConnection(Container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"create database [{databaseName}];";
        await command.ExecuteNonQueryAsync(cancellationToken);

        var builder = new SqlConnectionStringBuilder(Container.GetConnectionString())
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true,
        };

        return builder.ConnectionString;
    }
}
