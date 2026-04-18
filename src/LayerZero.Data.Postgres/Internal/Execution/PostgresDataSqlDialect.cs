using System.Globalization;
using System.Text;
using LayerZero.Data.Internal.Execution;
using LayerZero.Data.Internal.Sql;
using LayerZero.Data.Internal.Translation;
using LayerZero.Data.Postgres.Configuration;
using Microsoft.Extensions.Options;

namespace LayerZero.Data.Postgres.Internal.Execution;

internal sealed class PostgresDataSqlDialect(IOptions<PostgresDataOptions> optionsAccessor) : IDataSqlDialect
{
    private readonly PostgresDataOptions options = optionsAccessor.Value;

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

        return new CompiledDataCommandTemplate(
            BuildSelectQuery(
                template.Root,
                template.Joins,
                template.Filter,
                template.Orderings,
                template.Skip,
                effectiveTake,
                template.Projections),
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
                builder.Append("select exists(select 1 from (");
                builder.Append(innerQuery);
                builder.Append(") as \"q\")");
                break;
            case DataAggregateKind.Count:
                builder.Append("select cast(count(1) as integer) from (");
                builder.Append(innerQuery);
                builder.Append(") as \"q\"");
                break;
            case DataAggregateKind.LongCount:
                builder.Append("select count(1) from (");
                builder.Append(innerQuery);
                builder.Append(") as \"q\"");
                break;
            case DataAggregateKind.Sum:
                builder.Append("select sum(\"q\".\"value\") from (");
                builder.Append(innerQuery);
                builder.Append(") as \"q\"");
                break;
            case DataAggregateKind.Min:
                builder.Append("select min(\"q\".\"value\") from (");
                builder.Append(innerQuery);
                builder.Append(") as \"q\"");
                break;
            case DataAggregateKind.Max:
                builder.Append("select max(\"q\".\"value\") from (");
                builder.Append(innerQuery);
                builder.Append(") as \"q\"");
                break;
            default:
                throw new InvalidOperationException($"Unsupported aggregate '{template.Aggregate}'.");
        }

        return new CompiledDataCommandTemplate(builder.ToString(), CreateParameters(template.ParameterTypes), ["value"]);
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
            AppendDelimited(builder, template.Values.Select(static value => Placeholder(value.Ordinal)));
            builder.Append(");");
        }

        return new CompiledDataCommandTemplate(builder.ToString(), CreateParameters(template.ParameterTypes), []);
    }

    public CompiledDataCommandTemplate CompileUpdate(DataUpdateCommandTemplate template)
    {
        var builder = new StringBuilder();
        builder.Append("update ");
        AppendQualifiedTable(builder, template.Table.Name);
        builder.Append(" as \"t0\" set ");
        AppendDelimited(builder, template.Assignments.Select(assignment =>
            $"{QuoteIdentifier(assignment.Column.Name)} = {Placeholder(assignment.Ordinal)}"));

        if (template.Filter is not null)
        {
            builder.Append(" where ");
            builder.Append(RenderPredicate(template.Filter));
        }

        builder.Append(';');
        return new CompiledDataCommandTemplate(builder.ToString(), CreateParameters(template.ParameterTypes), []);
    }

    public CompiledDataCommandTemplate CompileDelete(DataDeleteCommandTemplate template)
    {
        var builder = new StringBuilder();
        builder.Append("delete from ");
        AppendQualifiedTable(builder, template.Table.Name);
        builder.Append(" as \"t0\"");

        if (template.Filter is not null)
        {
            builder.Append(" where ");
            builder.Append(RenderPredicate(template.Filter));
        }

        builder.Append(';');
        return new CompiledDataCommandTemplate(builder.ToString(), CreateParameters(template.ParameterTypes), []);
    }

    public CompiledDataCommandTemplate CompileRawSql(DataSqlStatement statement)
    {
        return new CompiledDataCommandTemplate(
            DataSqlStatementCompiler.RewriteCommandText(statement, Placeholder),
            statement.Parameters.Select((parameter, index) => new DataCommandParameterDescriptor(
                Placeholder(index),
                ParameterName: null,
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

        if (orderings.Count > 0)
        {
            builder.Append(" order by ");
            AppendDelimited(builder, orderings.Select(ordering =>
                $"{RenderExpression(ordering.Expression)} {(ordering.Descending ? "desc" : "asc")}"));
        }

        if (take.HasValue)
        {
            builder.Append(" limit ");
            builder.Append(take.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (skip.HasValue)
        {
            builder.Append(" offset ");
            builder.Append(skip.Value.ToString(CultureInfo.InvariantCulture));
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
                => $"({RenderExpression(column)} = true)",
            DataParameterExpressionTemplate parameter when IsBooleanType(parameter.ValueType)
                => $"({RenderExpression(parameter)} = true)",
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
            DataBinaryOperator.Equal => $"({leftText} is not distinct from {rightText})",
            DataBinaryOperator.NotEqual => $"({leftText} is distinct from {rightText})",
            _ => throw new InvalidOperationException($"Unsupported comparison '{comparison}'."),
        };
    }

    private string RenderExpression(DataExpressionTemplate expression, bool parameterizeProjectionLiterals = true)
    {
        return expression switch
        {
            DataColumnExpressionTemplate column => $"{QuoteIdentifier(column.Alias)}.{QuoteIdentifier(column.Column.Name)}",
            DataParameterExpressionTemplate parameter when parameter.Ordinal >= 0 => Placeholder(parameter.Ordinal),
            DataParameterExpressionTemplate => parameterizeProjectionLiterals ? Placeholder(0) : "1",
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
            "Contains" => $"({arguments[0]} like ('%' || {arguments[1]} || '%'))",
            "StartsWith" => $"({arguments[0]} like ({arguments[1]} || '%'))",
            "EndsWith" => $"({arguments[0]} like ('%' || {arguments[1]}))",
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
            .Select((type, index) => new DataCommandParameterDescriptor(Placeholder(index), ParameterName: null, type))
            .ToArray();

    private static string Placeholder(int ordinal) => $"${(ordinal + 1).ToString(CultureInfo.InvariantCulture)}";

    private static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

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
