using LayerZero.Data.Internal.Sql;
using LayerZero.Data.Internal.Translation;
using LayerZero.Data.Postgres.Configuration;
using LayerZero.Data.Postgres.Internal.Execution;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.Postgres.Tests.Execution;

public sealed class PostgresDataSqlDialectTests
{
    [Fact]
    public void CompileInsert_uses_default_schema_for_inferred_tables()
    {
        var dialect = CreateDialect(defaultSchema: "sales");

        var command = dialect.CompileInsert(DataCommandTranslation.CreateInsertTemplate(new PostgresConventionEntityMap().Table));

        Assert.Equal(
            "insert into \"sales\".\"PostgresConventionEntity\" (\"Id\", \"CustomerEmail\") values ($1, $2);",
            command.CommandText);
    }

    [Fact]
    public void CompileReader_renders_limit_and_offset_when_skip_and_take_are_present()
    {
        var dialect = CreateDialect();
        var table = new PostgresConventionEntityMap().Table;
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

        Assert.Contains("order by \"t0\".\"CustomerEmail\" asc limit 5 offset 10", command.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void CompileRawSql_rewrites_parameter_tokens_to_native_positional_placeholders()
    {
        var dialect = CreateDialect();
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var statement = CreateStatement($"select * from orders where id = {id} and total > {12.5m}");

        var command = dialect.CompileRawSql(statement);

        Assert.Equal("select * from orders where id = $1 and total > $2", command.CommandText);
        Assert.All(command.Parameters, static parameter => Assert.Null(parameter.ParameterName));
    }

    private static PostgresDataSqlDialect CreateDialect(string defaultSchema = "public") =>
        new(Options.Create(new PostgresDataOptions
        {
            ConnectionString = "Host=localhost;Database=fake;Username=postgres;Password=postgres",
            DefaultSchema = defaultSchema,
        }));

    private static DataSqlStatement CreateStatement(DataSqlInterpolatedStringHandler sql) => sql.Build();

    private sealed record PostgresConventionEntity(Guid Id, string CustomerEmail);

    private sealed class PostgresConventionEntityMap : EntityMap<PostgresConventionEntity>
    {
        protected override void Configure(EntityMapBuilder<PostgresConventionEntity> builder)
        {
            builder.Property(entity => entity.Id).IsKeyPart();
            builder.Property(entity => entity.CustomerEmail).HasStringType(256).IsRequired();
        }
    }
}
