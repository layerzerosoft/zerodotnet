using System.Linq.Expressions;
using System.Globalization;
using LayerZero.Data.Internal.Execution;
using LayerZero.Data.Internal.Registration;
using LayerZero.Data.Internal.Sql;

namespace LayerZero.Data.Internal.Translation;

internal enum DataBinaryOperator
{
    Equal = 0,
    NotEqual = 1,
    GreaterThan = 2,
    GreaterThanOrEqual = 3,
    LessThan = 4,
    LessThanOrEqual = 5,
    AndAlso = 6,
    OrElse = 7,
    Add = 8,
    Subtract = 9,
    Multiply = 10,
    Divide = 11,
}

internal enum DataUnaryOperator
{
    Not = 0,
    Negate = 1,
}

internal abstract record DataExpressionTemplate;

internal sealed record DataColumnExpressionTemplate(
    string Alias,
    IEntityColumn Column) : DataExpressionTemplate;

internal sealed record DataParameterExpressionTemplate(
    int Ordinal,
    Type ValueType) : DataExpressionTemplate;

internal sealed record DataBinaryExpressionTemplate(
    DataBinaryOperator Operator,
    DataExpressionTemplate Left,
    DataExpressionTemplate Right) : DataExpressionTemplate;

internal sealed record DataUnaryExpressionTemplate(
    DataUnaryOperator Operator,
    DataExpressionTemplate Operand) : DataExpressionTemplate;

internal sealed record DataFunctionExpressionTemplate(
    string Name,
    IReadOnlyList<DataExpressionTemplate> Arguments) : DataExpressionTemplate;

internal sealed record DataProjectionItemTemplate(
    string Alias,
    DataExpressionTemplate Expression);

internal sealed record DataOrderingExpressionTemplate(
    DataExpressionTemplate Expression,
    bool Descending);

internal sealed record DataTableSourceTemplate(
    string Alias,
    IEntityTable Table);

internal sealed record DataJoinSourceTemplate(
    string Alias,
    IEntityTable Table,
    DataExpressionTemplate LeftKey,
    DataExpressionTemplate RightKey);

internal sealed record DataReaderCommandTemplate(
    DataTableSourceTemplate Root,
    IReadOnlyList<DataJoinSourceTemplate> Joins,
    DataExpressionTemplate? Filter,
    IReadOnlyList<DataOrderingExpressionTemplate> Orderings,
    int? Skip,
    int? Take,
    IReadOnlyList<DataProjectionItemTemplate> Projections,
    Type ResultType,
    IReadOnlyList<Type> ParameterTypes);

internal sealed record DataAggregateCommandTemplate(
    DataTableSourceTemplate Root,
    IReadOnlyList<DataJoinSourceTemplate> Joins,
    DataExpressionTemplate? Filter,
    IReadOnlyList<DataOrderingExpressionTemplate> Orderings,
    int? Skip,
    int? Take,
    DataAggregateKind Aggregate,
    DataExpressionTemplate? Selector,
    Type ResultType,
    IReadOnlyList<Type> ParameterTypes);

internal sealed record DataInsertValueTemplate(
    IEntityColumn Column,
    int Ordinal);

internal sealed record DataInsertCommandTemplate(
    IEntityTable Table,
    IReadOnlyList<DataInsertValueTemplate> Values,
    IReadOnlyList<Type> ParameterTypes);

internal sealed record DataAssignmentTemplate(
    IEntityColumn Column,
    int Ordinal);

internal sealed record DataUpdateCommandTemplate(
    IEntityTable Table,
    IReadOnlyList<DataAssignmentTemplate> Assignments,
    DataExpressionTemplate? Filter,
    IReadOnlyList<Type> ParameterTypes);

internal sealed record DataDeleteCommandTemplate(
    IEntityTable Table,
    DataExpressionTemplate? Filter,
    IReadOnlyList<Type> ParameterTypes);

internal static class DataCommandTranslation
{
    public static string CreateReaderCacheKey(
        DataQueryModel model,
        LambdaExpression? projection,
        DataReadMode mode,
        Type resultType)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{mode}|{resultType.AssemblyQualifiedName}|{model.RootEntityType.AssemblyQualifiedName}|{model.RowType.AssemblyQualifiedName}|{string.Join("|", model.Joins.Select(static join => $"{join.RightEntityType.AssemblyQualifiedName}:{join.LeftKey.Body}:{join.RightKey.Body}"))}|{string.Join("|", model.Filters.Select(static filter => filter.Body.ToString()))}|{string.Join("|", model.Orderings.Select(static ordering => $"{ordering.Selector.Body}:{ordering.Descending}"))}|{model.Skip?.ToString(CultureInfo.InvariantCulture) ?? "-"}|{model.Take?.ToString(CultureInfo.InvariantCulture) ?? "-"}|{projection?.Body.ToString() ?? "-"}");
    }

    public static string CreateAggregateCacheKey(
        DataQueryModel model,
        DataAggregateKind aggregate,
        LambdaExpression? selector,
        Type resultType)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"aggregate:{aggregate}|{resultType.AssemblyQualifiedName}|{CreateReaderCacheKey(model, projection: selector, DataReadMode.List, resultType)}");
    }

    public static string CreateUpdateCacheKey(DataUpdateModel model)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"update|{model.EntityType.AssemblyQualifiedName}|{string.Join("|", model.Assignments.Select(static assignment => assignment.Property.Body.ToString()))}|{string.Join("|", model.Filters.Select(static filter => filter.Body.ToString()))}");
    }

    public static string CreateDeleteCacheKey(DataDeleteModel model)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"delete|{model.EntityType.AssemblyQualifiedName}|{string.Join("|", model.Filters.Select(static filter => filter.Body.ToString()))}");
    }

    public static string CreateInsertCacheKey(IEntityTable table)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"insert|{table.EntityType.AssemblyQualifiedName}|{string.Join("|", table.Columns.Where(static column => !column.Definition.IsIdentity).Select(static column => column.Name))}");
    }

    public static DataReaderCommandTemplate CreateReaderTemplate<TResult>(
        DataQueryModel model,
        LambdaExpression? projection,
        IEntityMapRegistry mapRegistry)
    {
        var parameterScope = new ParameterScope();
        var shapeBuilder = ShapeBuilder.Create(model, mapRegistry, parameterScope);
        var filter = BuildFilter(model, shapeBuilder.RowShape, parameterScope);
        var orderings = model.Orderings
            .Select(ordering => new DataOrderingExpressionTemplate(
                TranslateScalar(ordering.Selector.Body, shapeBuilder.RowShape, ordering.Selector.Parameters[0], parameterScope),
                ordering.Descending))
            .ToArray();
        var projections = projection is null
            ? BuildDefaultProjection(shapeBuilder.RowShape)
            : BuildProjection(projection, shapeBuilder.RowShape, parameterScope);

        return new DataReaderCommandTemplate(
            shapeBuilder.Root,
            shapeBuilder.Joins,
            filter,
            orderings,
            model.Skip,
            model.Take,
            projections,
            typeof(TResult),
            parameterScope.Types.ToArray());
    }

    public static DataAggregateCommandTemplate CreateAggregateTemplate<TResult>(
        DataQueryModel model,
        DataAggregateKind aggregate,
        LambdaExpression? selector,
        IEntityMapRegistry mapRegistry)
    {
        var parameterScope = new ParameterScope();
        var shapeBuilder = ShapeBuilder.Create(model, mapRegistry, parameterScope);
        var filter = BuildFilter(model, shapeBuilder.RowShape, parameterScope);
        var orderings = model.Orderings
            .Select(ordering => new DataOrderingExpressionTemplate(
                TranslateScalar(ordering.Selector.Body, shapeBuilder.RowShape, ordering.Selector.Parameters[0], parameterScope),
                ordering.Descending))
            .ToArray();
        var selectorExpression = selector is null
            ? null
            : TranslateScalar(selector.Body, shapeBuilder.RowShape, selector.Parameters[0], parameterScope);

        return new DataAggregateCommandTemplate(
            shapeBuilder.Root,
            shapeBuilder.Joins,
            filter,
            orderings,
            model.Skip,
            model.Take,
            aggregate,
            selectorExpression,
            typeof(TResult),
            parameterScope.Types.ToArray());
    }

    public static DataInsertCommandTemplate CreateInsertTemplate<TEntity>(EntityTable<TEntity> table)
        where TEntity : notnull
    {
        var values = new List<DataInsertValueTemplate>();
        var parameterTypes = new List<Type>();

        foreach (var column in table.Columns.Where(static column => !column.Definition.IsIdentity))
        {
            values.Add(new DataInsertValueTemplate(column, values.Count));
            parameterTypes.Add(column.ClrType);
        }

        return new DataInsertCommandTemplate(table, values, parameterTypes);
    }

    public static DataUpdateCommandTemplate CreateUpdateTemplate(
        DataUpdateModel model,
        IEntityMapRegistry mapRegistry)
    {
        var table = mapRegistry.GetTable(model.EntityType);
        var parameterTypes = new List<Type>();
        var assignments = new List<DataAssignmentTemplate>(model.Assignments.Count);

        foreach (var assignment in model.Assignments)
        {
            var property = ExpressionHelpers.GetProperty(assignment.Property);
            var column = table.Columns.FirstOrDefault(current => current.PropertyName.Equals(property.Name, StringComparison.Ordinal));
            if (column is null)
            {
                throw new InvalidOperationException($"Property '{property.Name}' is not mapped on entity '{model.EntityType.FullName}'.");
            }

            assignments.Add(new DataAssignmentTemplate(column, assignments.Count));
            parameterTypes.Add(assignment.ValueType);
        }

        var allParameterTypes = new List<Type>(parameterTypes);
        var parameterScope = new ParameterScope(assignments.Count, allParameterTypes);
        var rowShape = new EntityRowShape(table, "t0");
        var filter = BuildFilter(model.Filters, rowShape, parameterScope);

        return new DataUpdateCommandTemplate(table, assignments, filter, allParameterTypes.ToArray());
    }

    public static DataDeleteCommandTemplate CreateDeleteTemplate(
        DataDeleteModel model,
        IEntityMapRegistry mapRegistry)
    {
        var table = mapRegistry.GetTable(model.EntityType);
        var parameterScope = new ParameterScope();
        var rowShape = new EntityRowShape(table, "t0");
        var filter = BuildFilter(model.Filters, rowShape, parameterScope);
        return new DataDeleteCommandTemplate(table, filter, parameterScope.Types.ToArray());
    }

    public static object?[] CollectReaderParameterValues(
        DataQueryModel model,
        LambdaExpression? projection)
    {
        var values = new List<object?>();
        foreach (var join in model.Joins)
        {
            CollectExpressionValues(join.LeftKey.Body, join.LeftKey.Parameters[0], values);
            CollectExpressionValues(join.RightKey.Body, join.RightKey.Parameters[0], values);
        }

        foreach (var filter in model.Filters)
        {
            CollectExpressionValues(filter.Body, filter.Parameters[0], values);
        }

        foreach (var ordering in model.Orderings)
        {
            CollectExpressionValues(ordering.Selector.Body, ordering.Selector.Parameters[0], values);
        }

        if (projection is not null)
        {
            CollectProjectionValues(projection.Body, projection.Parameters[0], values);
        }

        return [.. values];
    }

    public static object?[] CollectAggregateParameterValues(
        DataQueryModel model,
        LambdaExpression? selector)
    {
        var values = new List<object?>(CollectReaderParameterValues(model, projection: null));
        if (selector is not null)
        {
            CollectExpressionValues(selector.Body, selector.Parameters[0], values);
        }

        return [.. values];
    }

    public static object?[] CollectInsertParameterValues<TEntity>(TEntity entity, EntityTable<TEntity> table)
        where TEntity : notnull
    {
        return table.Columns
            .Where(static column => !column.Definition.IsIdentity)
            .Select(column => column.GetProviderValue(entity))
            .ToArray();
    }

    public static object?[] CollectUpdateParameterValues(DataUpdateModel model)
    {
        var values = new List<object?>(model.Assignments.Select(static assignment => assignment.Value));
        foreach (var filter in model.Filters)
        {
            CollectExpressionValues(filter.Body, filter.Parameters[0], values);
        }

        return [.. values];
    }

    public static object?[] CollectDeleteParameterValues(DataDeleteModel model)
    {
        var values = new List<object?>();
        foreach (var filter in model.Filters)
        {
            CollectExpressionValues(filter.Body, filter.Parameters[0], values);
        }

        return [.. values];
    }

    private static DataExpressionTemplate? BuildFilter(
        DataQueryModel model,
        DataRowShape rowShape,
        ParameterScope parameterScope)
    {
        return BuildFilter(model.Filters, rowShape, parameterScope);
    }

    private static DataExpressionTemplate? BuildFilter(
        IReadOnlyList<LambdaExpression> filters,
        DataRowShape rowShape,
        ParameterScope parameterScope)
    {
        DataExpressionTemplate? filter = null;
        foreach (var predicate in filters)
        {
            var translated = TranslateScalar(predicate.Body, rowShape, predicate.Parameters[0], parameterScope);
            filter = filter is null
                ? translated
                : new DataBinaryExpressionTemplate(DataBinaryOperator.AndAlso, filter, translated);
        }

        return filter;
    }

    private static IReadOnlyList<DataProjectionItemTemplate> BuildProjection(
        LambdaExpression projection,
        DataRowShape rowShape,
        ParameterScope parameterScope)
    {
        return BuildProjectionExpression(projection.Body, rowShape, projection.Parameters[0], parameterScope);
    }

    private static IReadOnlyList<DataProjectionItemTemplate> BuildProjectionExpression(
        Expression expression,
        DataRowShape rowShape,
        ParameterExpression parameter,
        ParameterScope parameterScope)
    {
        expression = StripConvert(expression);

        if (expression == parameter)
        {
            return BuildDefaultProjection(rowShape);
        }

        if (expression is NewExpression created)
        {
            return BuildNewProjection(created, rowShape, parameter, parameterScope);
        }

        if (expression is MemberInitExpression initialized)
        {
            return initialized.Bindings
                .OfType<MemberAssignment>()
                .Select(binding => new DataProjectionItemTemplate(
                    binding.Member.Name,
                    TranslateScalar(binding.Expression, rowShape, parameter, parameterScope)))
                .ToArray();
        }

        return
        [
            new DataProjectionItemTemplate(
                InferAlias(expression) ?? "value",
                TranslateScalar(expression, rowShape, parameter, parameterScope)),
        ];
    }

    private static IReadOnlyList<DataProjectionItemTemplate> BuildNewProjection(
        NewExpression expression,
        DataRowShape rowShape,
        ParameterExpression parameter,
        ParameterScope parameterScope)
    {
        var constructorParameters = expression.Constructor?.GetParameters() ?? [];
        var items = new List<DataProjectionItemTemplate>(expression.Arguments.Count);
        for (var index = 0; index < expression.Arguments.Count; index++)
        {
            var alias = expression.Members?[index].Name
                ?? constructorParameters.ElementAtOrDefault(index)?.Name
                ?? $"item{index.ToString(CultureInfo.InvariantCulture)}";
            items.Add(new DataProjectionItemTemplate(
                alias,
                TranslateScalar(expression.Arguments[index], rowShape, parameter, parameterScope)));
        }

        return items;
    }

    private static IReadOnlyList<DataProjectionItemTemplate> BuildDefaultProjection(DataRowShape rowShape, string prefix = "")
    {
        if (rowShape is EntityRowShape entity)
        {
            return entity.Table.Columns
                .Select(column => new DataProjectionItemTemplate(prefix + column.Name, new DataColumnExpressionTemplate(entity.Alias, column)))
                .ToArray();
        }

        var join = (JoinRowShape)rowShape;
        return BuildDefaultProjection(join.Left, prefix + "l__")
            .Concat(BuildDefaultProjection(join.Right, prefix + "r__"))
            .ToArray();
    }

    private static DataExpressionTemplate TranslateScalar(
        Expression expression,
        DataRowShape rowShape,
        ParameterExpression parameter,
        ParameterScope parameterScope)
    {
        expression = StripConvert(expression);
        if (!ContainsParameter(expression, parameter))
        {
            return parameterScope.Next(expression.Type);
        }

        switch (expression)
        {
            case MemberExpression member when TryResolveColumn(member, rowShape, parameter, out var column):
                return column;
            case BinaryExpression binary:
                return new DataBinaryExpressionTemplate(
                    MapBinaryOperator(binary.NodeType),
                    TranslateScalar(binary.Left, rowShape, parameter, parameterScope),
                    TranslateScalar(binary.Right, rowShape, parameter, parameterScope));
            case UnaryExpression unary when unary.NodeType is ExpressionType.Not or ExpressionType.Negate or ExpressionType.NegateChecked:
                return new DataUnaryExpressionTemplate(
                    unary.NodeType == ExpressionType.Not ? DataUnaryOperator.Not : DataUnaryOperator.Negate,
                    TranslateScalar(unary.Operand, rowShape, parameter, parameterScope));
            case MethodCallExpression method:
                return TranslateMethodCall(method, rowShape, parameter, parameterScope);
            case ParameterExpression:
                throw new InvalidOperationException("Whole-row expressions are only supported through default projections.");
            default:
                throw new InvalidOperationException($"Expression '{expression}' is not supported by LayerZero.Data.");
        }
    }

    private static DataExpressionTemplate TranslateMethodCall(
        MethodCallExpression method,
        DataRowShape rowShape,
        ParameterExpression parameter,
        ParameterScope parameterScope)
    {
        if (method.Method.Name.Equals("Equals", StringComparison.Ordinal))
        {
            return method.Object is not null
                ? new DataBinaryExpressionTemplate(
                    DataBinaryOperator.Equal,
                    TranslateScalar(method.Object, rowShape, parameter, parameterScope),
                    TranslateScalar(method.Arguments[0], rowShape, parameter, parameterScope))
                : new DataBinaryExpressionTemplate(
                    DataBinaryOperator.Equal,
                    TranslateScalar(method.Arguments[0], rowShape, parameter, parameterScope),
                    TranslateScalar(method.Arguments[1], rowShape, parameter, parameterScope));
        }

        if (method.Object is not null
            && method.Object.Type == typeof(string)
            && method.Arguments.Count == 1
            && (method.Method.Name.Equals("Contains", StringComparison.Ordinal)
                || method.Method.Name.Equals("StartsWith", StringComparison.Ordinal)
                || method.Method.Name.Equals("EndsWith", StringComparison.Ordinal)))
        {
            return new DataFunctionExpressionTemplate(
                method.Method.Name,
                [
                    TranslateScalar(method.Object, rowShape, parameter, parameterScope),
                    TranslateScalar(method.Arguments[0], rowShape, parameter, parameterScope),
                ]);
        }

        throw new InvalidOperationException($"Method '{method.Method.Name}' is not supported by LayerZero.Data.");
    }

    private static bool TryResolveColumn(
        MemberExpression member,
        DataRowShape rowShape,
        ParameterExpression parameter,
        out DataColumnExpressionTemplate column)
    {
        var path = GetMemberPath(member, parameter);
        if (path.Count == 0)
        {
            column = default!;
            return false;
        }

        column = ResolvePath(path, rowShape);
        return true;
    }

    private static DataColumnExpressionTemplate ResolvePath(IReadOnlyList<string> path, DataRowShape rowShape)
    {
        if (rowShape is EntityRowShape entity)
        {
            if (path.Count != 1)
            {
                throw new InvalidOperationException($"Path '{string.Join(".", path)}' does not resolve to a mapped column.");
            }

            var column = entity.Table.Columns.FirstOrDefault(current => current.PropertyName.Equals(path[0], StringComparison.Ordinal));
            if (column is null)
            {
                throw new InvalidOperationException($"Property '{path[0]}' is not mapped on entity '{entity.Table.EntityType.FullName}'.");
            }

            return new DataColumnExpressionTemplate(entity.Alias, column);
        }

        var join = (JoinRowShape)rowShape;
        return path[0] switch
        {
            "Left" => ResolvePath(path.Skip(1).ToArray(), join.Left),
            "Right" => ResolvePath(path.Skip(1).ToArray(), join.Right),
            _ => throw new InvalidOperationException($"Joined row path '{string.Join(".", path)}' must use Left/Right navigation."),
        };
    }

    private static IReadOnlyList<string> GetMemberPath(Expression expression, ParameterExpression parameter)
    {
        expression = StripConvert(expression);
        var segments = new Stack<string>();
        while (expression is MemberExpression member)
        {
            segments.Push(member.Member.Name);
            expression = StripConvert(member.Expression!);
        }

        return expression == parameter
            ? segments.ToArray()
            : [];
    }

    private static bool ContainsParameter(Expression expression, ParameterExpression parameter)
    {
        var found = false;
        new ParameterSearchVisitor(parameter, () => found = true).Visit(expression);
        return found;
    }

    private static void CollectProjectionValues(Expression expression, ParameterExpression parameter, ICollection<object?> values)
    {
        expression = StripConvert(expression);
        switch (expression)
        {
            case NewExpression created:
                foreach (var argument in created.Arguments)
                {
                    CollectExpressionValues(argument, parameter, values);
                }

                return;
            case MemberInitExpression initialized:
                foreach (var binding in initialized.Bindings.OfType<MemberAssignment>())
                {
                    CollectExpressionValues(binding.Expression, parameter, values);
                }

                return;
            default:
                CollectExpressionValues(expression, parameter, values);
                return;
        }
    }

    private static void CollectExpressionValues(Expression expression, ParameterExpression parameter, ICollection<object?> values)
    {
        expression = StripConvert(expression);
        if (!ContainsParameter(expression, parameter))
        {
            values.Add(Evaluate(expression));
            return;
        }

        switch (expression)
        {
            case MemberExpression member when GetMemberPath(member, parameter).Count > 0:
                return;
            case BinaryExpression binary:
                CollectExpressionValues(binary.Left, parameter, values);
                CollectExpressionValues(binary.Right, parameter, values);
                return;
            case UnaryExpression unary when unary.NodeType is ExpressionType.Not or ExpressionType.Negate or ExpressionType.NegateChecked:
                CollectExpressionValues(unary.Operand, parameter, values);
                return;
            case MethodCallExpression method when method.Object is not null:
                CollectExpressionValues(method.Object, parameter, values);
                foreach (var argument in method.Arguments)
                {
                    CollectExpressionValues(argument, parameter, values);
                }

                return;
            case MethodCallExpression method:
                foreach (var argument in method.Arguments)
                {
                    CollectExpressionValues(argument, parameter, values);
                }

                return;
            case ParameterExpression:
                return;
            default:
                throw new InvalidOperationException($"Expression '{expression}' is not supported by LayerZero.Data.");
        }
    }

    private static object? Evaluate(Expression expression)
    {
        var convert = Expression.Convert(expression, typeof(object));
        return Expression.Lambda<Func<object?>>(convert).Compile().Invoke();
    }

    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }

    private static string? InferAlias(Expression expression)
    {
        expression = StripConvert(expression);
        return expression switch
        {
            MemberExpression member => member.Member.Name,
            _ => null,
        };
    }

    private static DataBinaryOperator MapBinaryOperator(ExpressionType expressionType) =>
        expressionType switch
        {
            ExpressionType.Equal => DataBinaryOperator.Equal,
            ExpressionType.NotEqual => DataBinaryOperator.NotEqual,
            ExpressionType.GreaterThan => DataBinaryOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => DataBinaryOperator.GreaterThanOrEqual,
            ExpressionType.LessThan => DataBinaryOperator.LessThan,
            ExpressionType.LessThanOrEqual => DataBinaryOperator.LessThanOrEqual,
            ExpressionType.AndAlso => DataBinaryOperator.AndAlso,
            ExpressionType.OrElse => DataBinaryOperator.OrElse,
            ExpressionType.Add => DataBinaryOperator.Add,
            ExpressionType.Subtract => DataBinaryOperator.Subtract,
            ExpressionType.Multiply => DataBinaryOperator.Multiply,
            ExpressionType.Divide => DataBinaryOperator.Divide,
            _ => throw new InvalidOperationException($"Binary operator '{expressionType}' is not supported by LayerZero.Data."),
        };

    private sealed class ShapeBuilder
    {
        private ShapeBuilder(
            DataTableSourceTemplate root,
            IReadOnlyList<DataJoinSourceTemplate> joins,
            DataRowShape rowShape)
        {
            Root = root;
            Joins = joins;
            RowShape = rowShape;
        }

        public DataTableSourceTemplate Root { get; }

        public IReadOnlyList<DataJoinSourceTemplate> Joins { get; }

        public DataRowShape RowShape { get; }

        public static ShapeBuilder Create(
            DataQueryModel model,
            IEntityMapRegistry mapRegistry,
            ParameterScope parameterScope)
        {
            var rootTable = mapRegistry.GetTable(model.RootEntityType);
            var root = new DataTableSourceTemplate("t0", rootTable);
            DataRowShape currentShape = new EntityRowShape(rootTable, root.Alias);
            var joins = new List<DataJoinSourceTemplate>(model.Joins.Count);

            for (var index = 0; index < model.Joins.Count; index++)
            {
                var join = model.Joins[index];
                var rightTable = mapRegistry.GetTable(join.RightEntityType);
                var rightAlias = $"t{index + 1}".ToString(CultureInfo.InvariantCulture);
                var rightShape = new EntityRowShape(rightTable, rightAlias);
                var leftKey = TranslateScalar(join.LeftKey.Body, currentShape, join.LeftKey.Parameters[0], parameterScope);
                var rightKey = TranslateScalar(join.RightKey.Body, rightShape, join.RightKey.Parameters[0], parameterScope);
                joins.Add(new DataJoinSourceTemplate(rightAlias, rightTable, leftKey, rightKey));
                currentShape = new JoinRowShape(currentShape, rightShape);
            }

            return new ShapeBuilder(root, joins, currentShape);
        }
    }

    private sealed class ParameterSearchVisitor(ParameterExpression parameter, Action found) : ExpressionVisitor
    {
        private readonly ParameterExpression parameter = parameter;
        private readonly Action found = found;

        public override Expression Visit(Expression? node)
        {
            if (node == parameter)
            {
                found();
            }

            return base.Visit(node)!;
        }
    }

    private sealed class ParameterScope
    {
        private readonly List<Type> types;

        public ParameterScope()
            : this(startingOrdinal: 0, types: [])
        {
        }

        public ParameterScope(int startingOrdinal, List<Type> types)
        {
            NextOrdinal = startingOrdinal;
            this.types = types;
        }

        public int NextOrdinal { get; private set; }

        public IReadOnlyList<Type> Types => types;

        public DataParameterExpressionTemplate Next(Type valueType)
        {
            types.Add(valueType);
            return new DataParameterExpressionTemplate(NextOrdinal++, valueType);
        }
    }
}

internal abstract record DataRowShape(Type Type);

internal sealed record EntityRowShape(
    IEntityTable Table,
    string Alias) : DataRowShape(Table.EntityType);

internal sealed record JoinRowShape(
    DataRowShape Left,
    DataRowShape Right) : DataRowShape(typeof(DataJoin<,>).MakeGenericType(Left.Type, Right.Type));
