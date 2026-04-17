using System.Globalization;
using System.Text;
using LayerZero.Data.Internal.Execution;
using LayerZero.Data.Internal.Sql;
using LayerZero.Data.Internal.Translation;
using LayerZero.Data.SqlServer.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.SqlServer.Internal.Execution;

internal sealed class SqlServerDataSqlDialect(IOptions<SqlServerDataOptions> optionsAccessor) : IDataSqlDialect
{
    private readonly SqlServerDataOptions options = optionsAccessor.Value;

    public CompiledDataCommandTemplate CompileReader(DataReaderCommandTemplate template, DataReadMode mode)
    {
        var effectiveTake = template.Take;
        if (mode is DataReadMode.First or DataReadMode.FirstOrDefault)
        {
            effectiveTake = effectiveTake.HasValue ? Math.Min(effectiveTake.Value, 1) : 1;
        }
        else if (mode is DataReadMode.Single or DataReadMode.SingleOrDefault)
        {
            effectiveTake = effectiveTake.HasValue ? Math.Min(effectiveTake.Value, 2) : 2;
        }

        var commandText = BuildSelectQuery(
            template.Root,
            template.Joins,
            template.Filter,
            template.Orderings,
            template.Skip,
            effectiveTake,
            template.Projections);

        return new CompiledDataCommandTemplate(
            commandText,
            CreateParameters(template.ParameterTypes),
            template.Projections.Select(static projection => projection.Alias).ToArray());
    }

    public CompiledDataCommandTemplate CompileAggregate(DataAggregateCommandTemplate template)
    {
        var effectiveOrderings = template.Skip.HasValue || template.Take.HasValue
            ? template.Orderings
            : [];

        IReadOnlyList<DataProjectionItemTemplate> innerProjection = template.Aggregate switch
        {
            DataAggregateKind.Any or DataAggregateKind.Count or DataAggregateKind.LongCount =>
            [
                new DataProjectionItemTemplate("value", new DataParameterExpressionTemplate(-1, typeof(int))),
            ],
            _ =>
            [
                new DataProjectionItemTemplate("value", template.Selector ?? throw new InvalidOperationException("Aggregate selector is required.")),
            ],
        };

        var innerQuery = BuildSelectQuery(
            template.Root,
            template.Joins,
            template.Filter,
            effectiveOrderings,
            template.Skip,
            template.Take,
            innerProjection,
            parameterizeProjectionLiterals: template.Aggregate is not DataAggregateKind.Any and not DataAggregateKind.Count and not DataAggregateKind.LongCount);

        var builder = new StringBuilder();
        switch (template.Aggregate)
        {
            case DataAggregateKind.Any:
                builder.Append("select cast(case when exists(select 1 from (");
                builder.Append(innerQuery);
                builder.Append(") as [q]) then 1 else 0 end as bit)");
                break;
            case DataAggregateKind.Count:
                builder.Append("select cast(count(1) as int) from (");
                builder.Append(innerQuery);
                builder.Append(") as [q]");
                break;
            case DataAggregateKind.LongCount:
                builder.Append("select count_big(1) from (");
                builder.Append(innerQuery);
                builder.Append(") as [q]");
                break;
            case DataAggregateKind.Sum:
                builder.Append("select sum([q].[value]) from (");
                builder.Append(innerQuery);
                builder.Append(") as [q]");
                break;
            case DataAggregateKind.Min:
                builder.Append("select min([q].[value]) from (");
                builder.Append(innerQuery);
                builder.Append(") as [q]");
                break;
            case DataAggregateKind.Max:
                builder.Append("select max([q].[value]) from (");
                builder.Append(innerQuery);
                builder.Append(") as [q]");
                break;
            default:
                throw new InvalidOperationException($"Unsupported aggregate '{template.Aggregate}'.");
        }

        return new CompiledDataCommandTemplate(
            builder.ToString(),
            CreateParameters(template.ParameterTypes),
            ["value"]);
    }

    public CompiledDataCommandTemplate CompileInsert(DataInsertCommandTemplate template)
    {
        var builder = new StringBuilder();
        builder.Append("insert into ");
        AppendQualifiedTable(builder, template.Table.Name);

        if (template.Values.Count == 0)
        {
            builder.Append(" default values;");
        }
        else
        {
            builder.Append(" (");
            AppendDelimited(builder, template.Values.Select(static value => QuoteIdentifier(value.Column.Name)));
            builder.Append(") values (");
            AppendDelimited(builder, template.Values.Select(static value => ParameterName(value.Ordinal)));
            builder.Append(");");
        }

        return new CompiledDataCommandTemplate(
            builder.ToString(),
            CreateParameters(template.ParameterTypes),
            []);
    }

    public CompiledDataCommandTemplate CompileUpdate(DataUpdateCommandTemplate template)
    {
        var builder = new StringBuilder();
        builder.Append("update [t0] set ");
        AppendDelimited(builder, template.Assignments.Select(assignment =>
            $"{QuoteIdentifier(assignment.Column.Name)} = {ParameterName(assignment.Ordinal)}"));
        builder.Append(" from ");
        AppendQualifiedTable(builder, template.Table.Name);
        builder.Append(" as [t0]");

        if (template.Filter is not null)
        {
            builder.Append(" where ");
            builder.Append(RenderPredicate(template.Filter));
        }

        builder.Append(';');

        return new CompiledDataCommandTemplate(
            builder.ToString(),
            CreateParameters(template.ParameterTypes),
            []);
    }

    public CompiledDataCommandTemplate CompileDelete(DataDeleteCommandTemplate template)
    {
        var builder = new StringBuilder();
        builder.Append("delete [t0] from ");
        AppendQualifiedTable(builder, template.Table.Name);
        builder.Append(" as [t0]");

        if (template.Filter is not null)
        {
            builder.Append(" where ");
            builder.Append(RenderPredicate(template.Filter));
        }

        builder.Append(';');

        return new CompiledDataCommandTemplate(
            builder.ToString(),
            CreateParameters(template.ParameterTypes),
            []);
    }

    public CompiledDataCommandTemplate CompileRawSql(DataSqlStatement statement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement.CommandText);

        return new CompiledDataCommandTemplate(
            statement.CommandText,
            statement.Parameters.Select(parameter => new DataCommandParameterDescriptor(
                parameter.Name,
                parameter.Value?.GetType() ?? typeof(object))).ToArray(),
            []);
    }

    private string BuildSelectQuery(
        DataTableSourceTemplate root,
        IReadOnlyList<DataJoinSourceTemplate> joins,
        DataExpressionTemplate? filter,
        IReadOnlyList<DataOrderingExpressionTemplate> orderings,
        int? skip,
        int? take,
        IReadOnlyList<DataProjectionItemTemplate> projections,
        bool parameterizeProjectionLiterals = true)
    {
        var builder = new StringBuilder();
        builder.Append("select ");

        var useTop = skip is null && take.HasValue;
        if (useTop)
        {
            builder.Append("top (");
            builder.Append(take.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));
            builder.Append(") ");
        }

        AppendDelimited(builder, projections.Select(projection =>
            $"{RenderExpression(projection.Expression, parameterizeProjectionLiterals)} as {QuoteIdentifier(projection.Alias)}"));

        builder.Append(" from ");
        AppendQualifiedTable(builder, root.Table.Name);
        builder.Append(" as ");
        builder.Append(QuoteIdentifier(root.Alias));

        foreach (var join in joins)
        {
            builder.Append(" inner join ");
            AppendQualifiedTable(builder, join.Table.Name);
            builder.Append(" as ");
            builder.Append(QuoteIdentifier(join.Alias));
            builder.Append(" on ");
            builder.Append(RenderComparison(join.LeftKey, join.RightKey, DataBinaryOperator.Equal));
        }

        if (filter is not null)
        {
            builder.Append(" where ");
            builder.Append(RenderPredicate(filter));
        }

        if (orderings.Count > 0 || skip.HasValue)
        {
            builder.Append(" order by ");
            if (orderings.Count == 0)
            {
                builder.Append("(select 1)");
            }
            else
            {
                AppendDelimited(builder, orderings.Select(ordering =>
                    $"{RenderExpression(ordering.Expression)} {(ordering.Descending ? "desc" : "asc")}"));
            }
        }

        if (skip.HasValue)
        {
            builder.Append(" offset ");
            builder.Append(skip.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(" rows");

            if (take.HasValue)
            {
                builder.Append(" fetch next ");
                builder.Append(take.Value.ToString(CultureInfo.InvariantCulture));
                builder.Append(" rows only");
            }
        }

        return builder.ToString();
    }

    private string RenderPredicate(DataExpressionTemplate expression)
    {
        return expression switch
        {
            DataBinaryExpressionTemplate binary when binary.Operator is DataBinaryOperator.AndAlso or DataBinaryOperator.OrElse
                => $"({RenderPredicate(binary.Left)} {RenderBinaryOperator(binary.Operator)} {RenderPredicate(binary.Right)})",
            DataBinaryExpressionTemplate binary when binary.Operator is DataBinaryOperator.Equal or DataBinaryOperator.NotEqual
                => RenderComparison(binary.Left, binary.Right, binary.Operator),
            DataBinaryExpressionTemplate binary
                => $"({RenderExpression(binary.Left)} {RenderBinaryOperator(binary.Operator)} {RenderExpression(binary.Right)})",
            DataUnaryExpressionTemplate { Operator: DataUnaryOperator.Not } unary
                => $"(not {RenderPredicate(unary.Operand)})",
            DataColumnExpressionTemplate column when IsBooleanType(column.Column.ClrType)
                => $"({RenderExpression(column)} = cast(1 as bit))",
            DataParameterExpressionTemplate parameter when IsBooleanType(parameter.ValueType)
                => $"({RenderExpression(parameter)} = cast(1 as bit))",
            DataFunctionExpressionTemplate function
                => RenderExpression(function),
            _ => RenderExpression(expression),
        };
    }

    private string RenderComparison(
        DataExpressionTemplate left,
        DataExpressionTemplate right,
        DataBinaryOperator comparison)
    {
        var leftText = RenderExpression(left);
        var rightText = RenderExpression(right);

        return comparison switch
        {
            DataBinaryOperator.Equal =>
                $"(({leftText} = {rightText}) or ({leftText} is null and {rightText} is null))",
            DataBinaryOperator.NotEqual =>
                $"(({leftText} <> {rightText}) or ({leftText} is null and {rightText} is not null) or ({leftText} is not null and {rightText} is null))",
            _ => throw new InvalidOperationException($"Unsupported comparison '{comparison}'."),
        };
    }

    private string RenderExpression(DataExpressionTemplate expression, bool parameterizeProjectionLiterals = true)
    {
        return expression switch
        {
            DataColumnExpressionTemplate column => $"{QuoteIdentifier(column.Alias)}.{QuoteIdentifier(column.Column.Name)}",
            DataParameterExpressionTemplate parameter when parameter.Ordinal >= 0 => ParameterName(parameter.Ordinal),
            DataParameterExpressionTemplate => parameterizeProjectionLiterals ? "@p0" : "1",
            DataBinaryExpressionTemplate binary when binary.Operator is DataBinaryOperator.Equal or DataBinaryOperator.NotEqual
                => RenderComparison(binary.Left, binary.Right, binary.Operator),
            DataBinaryExpressionTemplate binary
                => $"({RenderExpression(binary.Left)} {RenderBinaryOperator(binary.Operator)} {RenderExpression(binary.Right)})",
            DataUnaryExpressionTemplate { Operator: DataUnaryOperator.Not } unary
                => $"(not {RenderExpression(unary.Operand)})",
            DataUnaryExpressionTemplate unary
                => $"(-{RenderExpression(unary.Operand)})",
            DataFunctionExpressionTemplate function => RenderFunction(function),
            _ => throw new InvalidOperationException($"Unsupported expression '{expression.GetType().Name}'."),
        };
    }

    private string RenderFunction(DataFunctionExpressionTemplate function)
    {
        var arguments = function.Arguments.Select(argument => RenderExpression(argument)).ToArray();
        return function.Name switch
        {
            "Contains" => $"({arguments[0]} like ('%' + {arguments[1]} + '%'))",
            "StartsWith" => $"({arguments[0]} like ({arguments[1]} + '%'))",
            "EndsWith" => $"({arguments[0]} like ('%' + {arguments[1]}))",
            _ => throw new InvalidOperationException($"Unsupported function '{function.Name}'."),
        };
    }

    private static string RenderBinaryOperator(DataBinaryOperator dataBinaryOperator) =>
        dataBinaryOperator switch
        {
            DataBinaryOperator.AndAlso => "and",
            DataBinaryOperator.OrElse => "or",
            DataBinaryOperator.GreaterThan => ">",
            DataBinaryOperator.GreaterThanOrEqual => ">=",
            DataBinaryOperator.LessThan => "<",
            DataBinaryOperator.LessThanOrEqual => "<=",
            DataBinaryOperator.Add => "+",
            DataBinaryOperator.Subtract => "-",
            DataBinaryOperator.Multiply => "*",
            DataBinaryOperator.Divide => "/",
            _ => throw new InvalidOperationException($"Unsupported operator '{dataBinaryOperator}'."),
        };

    private void AppendQualifiedTable(StringBuilder builder, QualifiedTableName tableName)
    {
        if (!string.IsNullOrWhiteSpace(tableName.Schema ?? options.DefaultSchema))
        {
            builder.Append(QuoteIdentifier(tableName.Schema ?? options.DefaultSchema));
            builder.Append('.');
        }

        builder.Append(QuoteIdentifier(tableName.Name));
    }

    private static IReadOnlyList<DataCommandParameterDescriptor> CreateParameters(IReadOnlyList<Type> parameterTypes) =>
        parameterTypes
            .Select((type, index) => new DataCommandParameterDescriptor(ParameterName(index), type))
            .ToArray();

    private static string ParameterName(int ordinal) => $"@p{ordinal.ToString(CultureInfo.InvariantCulture)}";

    private static string QuoteIdentifier(string name) => $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static void AppendDelimited(StringBuilder builder, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(values);

        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            first = false;
            builder.Append(value);
        }
    }

    private static bool IsBooleanType(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) == typeof(bool);
}
