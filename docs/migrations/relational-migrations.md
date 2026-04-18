# LayerZero Relational Migrations

LayerZero relational migrations are code-first, forward-only, convention-first,
and built on the standalone `LayerZero.Data` foundation.

## Packages

- `LayerZero.Data`: provider-agnostic data foundation and typed relational
  mapping.
- `LayerZero.Data.SqlServer`: SQL Server provider registration and connection
  services.
- `LayerZero.Data.Postgres`: PostgreSQL provider registration,
  `NpgsqlDataSource` integration, and connection services.
- `LayerZero.Migrations`: provider-neutral migrations runtime, DSL, seed
  profiles, app-hosted commands, and internal analyzer/build assets for
  migration discovery.
- `LayerZero.Migrations.SqlServer`: SQL Server migration execution, SQL
  rendering, history storage, and locking.
- `LayerZero.Migrations.Postgres`: PostgreSQL migration execution, SQL
  rendering, history storage, and advisory locking.

`LayerZero.Generators` is not involved in migration discovery.

## Registration

Registration is data-first and single-path:

```csharp
using LayerZero.Data;
using LayerZero.Data.SqlServer;
using LayerZero.Migrations;

builder.Services
    .AddData(data =>
    {
        data.UseSqlServer(options =>
        {
            options.ConnectionStringName = "Main";
            options.DefaultSchema = "dbo";
        });
        data.UseMigrations(options =>
        {
            options.Executor = "orders-deploy";
        });
    });
```

Choose `UseSqlServer(...)` or `UsePostgres(...)` inside the same `AddData(...)`
registration block. Connection strings follow normal .NET configuration rules:

- `ConnectionStrings:Main` if `ConnectionStringName` is `Main`
- `LayerZero:Data:SqlServer:ConnectionString` or `LayerZero:Data:Postgres:ConnectionString` for an explicit provider setting
- `LayerZero:Data:ConnectionString` for a provider-neutral runtime override
- `--connection-string` only as a command-line override

## Authoring Conventions

Migrations and seeds are parameterless classes. Metadata is inferred from
folder and file conventions instead of handwritten constructors.

Artifacts:

- Migrations live under `Migrations/`.
- Seeds live under `Seeds/<profile>/`.
- File names use `yyyyMMddHHmmss_Name.cs`.
- Type names must match the file name suffix, with optional `Migration` or
  `Seed` suffixes.
- Display names are inferred from the type names.

Example migration:

```csharp
namespace Demo.App;

internal sealed class CreateAccountsMigration : Migration
{
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
```

Example seed:

```csharp
namespace Demo.App;

internal sealed class RolesSeed : Seed
{
    public override void Build(SeedBuilder builder)
    {
        builder.UpsertData(
            "roles",
            ["id"],
            row => row
                .Set("id", 1)
                .Set("name", "admin"));
    }
}
```

Profiles are first-class. `baseline` is the reserved safe default profile.
Additional profiles such as `dev`, `demo`, and `test` are opt-in.

## Typed Relational Mapping

`LayerZero.Data` provides typed maps that migrations can reuse for better table,
column, and index safety.

```csharp
using LayerZero.Data;

internal sealed record Account(Guid Id, string Email);

internal sealed class AccountMap : EntityMap<Account>
{
    protected override void Configure(EntityMapBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.Property(account => account.Id).IsKeyPart();
        builder.Property(account => account.Email).HasStringType(256).IsRequired();
        builder.HasIndex("IX_accounts_email", isUnique: true, account => account.Email);
    }
}

internal sealed class CreateAccountsMigration : Migration
{
    public override void Build(MigrationBuilder builder)
    {
        var map = new AccountMap().Table;
        builder.CreateTable(map);
        builder.CreateIndex(map, map.Indexes[0]);
    }
}
```

String-based operations and raw SQL remain available as escape hatches.

## Commands

The primary workflow is app-hosted commands, not a separate global tool.
Applications opt in by handing command execution to the host before normal
startup:

```csharp
if (await builder.RunLayerZeroMigrationsCommandAsync(args, builder.Build) is { } exitCode)
{
    return exitCode;
}
```

Scaffolding:

```bash
dotnet run -- migrations add CreateAccounts
dotnet run -- migrations add CreateLedger --non-transactional
dotnet run -- migrations add-seed Roles --profile baseline
dotnet run -- migrations add-seed DemoRoles --profile dev
```

Runtime commands:

```bash
dotnet run -- migrations info
dotnet run -- migrations validate
dotnet run -- migrations script --profile dev --include-baseline-seeds
dotnet run -- migrations apply --profile dev
dotnet run -- migrations baseline --profile dev --include-baseline-seeds
```

Optional CLI overrides:

```bash
dotnet run -- migrations info --connection-string "<sql-server-connection-string>"
dotnet run -- migrations script --output artifacts/migrations.sql
```

The reference host in `eng/LayerZero.Migrations.Runner` exists as a sample and
CI-friendly utility, but the intended public workflow is the same command
integration inside the target application host.

## Runtime Surface

`IMigrationRuntime` exposes:

- `InfoAsync`: reports local and applied migration/seed status.
- `ValidateAsync`: detects checksum drift, missing local definitions, duplicate
  ids, and out-of-order additions.
- `ScriptAsync`: generates reviewed SQL for `apply` or `baseline`.
- `ApplyAsync`: executes pending migrations and selected seed profiles.
- `BaselineAsync`: journals artifacts without executing them.

`ApplyAsync` refuses to take over a non-empty database without LayerZero
history. Existing databases must be baselined intentionally first.

## Provider Defaults

The SQL Server adapter currently provides:

- one schema-history table for migrations and seeds
- transaction-per-artifact execution by default
- per-migration non-transactional opt-out
- `SET XACT_ABORT ON`
- `sp_getapplock` runner serialization
- SQL Server-safe identifier quoting and literal rendering

The PostgreSQL adapter currently provides:

- one schema-history table for migrations and seeds
- transaction-per-artifact execution by default
- per-migration non-transactional opt-out
- advisory-lock runner serialization
- `CREATE SCHEMA IF NOT EXISTS`
- PostgreSQL-safe identifier quoting, `ON CONFLICT` upserts, and literal rendering
