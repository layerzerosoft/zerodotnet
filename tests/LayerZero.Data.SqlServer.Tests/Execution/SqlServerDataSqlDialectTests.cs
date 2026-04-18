using LayerZero.Data.Internal.Sql;
using LayerZero.Data.Internal.Translation;
using LayerZero.Data.SqlServer.Configuration;
using LayerZero.Data.SqlServer.Internal.Execution;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.SqlServer.Tests.Execution;

public sealed class SqlServerDataSqlDialectTests
{
    [Fact]
    public void CompileInsert_uses_default_schema_for_inferred_tables()
    {
        var dialect = new SqlServerDataSqlDialect(Options.Create(new SqlServerDataOptions
        {
            ConnectionString = "Server=(local);Database=fake;Trusted_Connection=True;",
            DefaultSchema = "sales",
        }));

        var command = dialect.CompileInsert(DataCommandTranslation.CreateInsertTemplate(new SqlServerConventionEntityMap().Table));

        Assert.Equal(
            "insert into [sales].[SqlServerConventionEntity] ([Id], [CustomerEmail]) values (@p0, @p1);",
            command.CommandText);
    }

    [Fact]
    public void CompileReader_renders_offset_fetch_when_skip_and_take_are_present()
    {
        var dialect = new SqlServerDataSqlDialect(Options.Create(new SqlServerDataOptions
        {
            ConnectionString = "Server=(local);Database=fake;Trusted_Connection=True;",
            DefaultSchema = "dbo",
        }));

        var table = new SqlServerConventionEntityMap().Table;
        var template = new DataReaderCommandTemplate(
            new DataTableSourceTemplate("t0", table),
            [],
            Filter: null,
            Orderings:
            [
                new DataOrderingExpressionTemplate(
                    new DataColumnExpressionTemplate("t0", table.Columns[1]),
                    Descending: false),
            ],
            Skip: 10,
            Take: 5,
            Projections:
            [
                new DataProjectionItemTemplate("CustomerEmail", new DataColumnExpressionTemplate("t0", table.Columns[1])),
            ],
            ResultType: typeof(string),
            ParameterTypes: []);

        var command = dialect.CompileReader(template, DataReadMode.List);

        Assert.Contains("order by [t0].[CustomerEmail] asc offset 10 rows fetch next 5 rows only", command.CommandText, StringComparison.Ordinal);
    }

    private sealed record SqlServerConventionEntity(Guid Id, string CustomerEmail);

    private sealed class SqlServerConventionEntityMap : EntityMap<SqlServerConventionEntity>
    {
        protected override void Configure(EntityMapBuilder<SqlServerConventionEntity> builder)
        {
            builder.Property(entity => entity.Id).IsKeyPart();
            builder.Property(entity => entity.CustomerEmail).HasStringType(256).IsRequired();
        }
    }
}
