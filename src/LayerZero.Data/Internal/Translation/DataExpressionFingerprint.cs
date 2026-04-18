using System.Globalization;
using System.Linq.Expressions;
using System.Text;

namespace LayerZero.Data.Internal.Translation;

internal static class DataExpressionFingerprint
{
    public static string Create(LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var writer = new Writer();
        writer.AppendLambda(expression);
        return writer.ToString();
    }

    public static string Create(Expression expression, IReadOnlyList<ParameterExpression> parameters)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(parameters);

        var writer = new Writer(parameters);
        writer.AppendExpression(expression);
        return writer.ToString();
    }

    public static string Create(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
    }

    private sealed class Writer : ExpressionVisitor
    {
        private readonly StringBuilder builder = new();
        private readonly Dictionary<ParameterExpression, int> parameterOrdinals;

        public Writer()
            : this([])
        {
        }

        public Writer(IReadOnlyList<ParameterExpression> parameters)
        {
            parameterOrdinals = parameters
                .Select((parameter, index) => new KeyValuePair<ParameterExpression, int>(parameter, index))
                .ToDictionary(static pair => pair.Key, static pair => pair.Value);
        }

        public void AppendLambda(LambdaExpression expression)
        {
            builder.Append("lambda(");
            for (var index = 0; index < expression.Parameters.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append("p");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(Create(expression.Parameters[index].Type));
                parameterOrdinals[expression.Parameters[index]] = index;
            }

            builder.Append(")=>");
            Visit(expression.Body);
        }

        public void AppendExpression(Expression expression) => Visit(expression);

        public override Expression Visit(Expression? node)
        {
            if (node is null)
            {
                builder.Append("null");
                return null!;
            }

            builder.Append('[');
            builder.Append(node.NodeType.ToString());
            builder.Append(':');
            builder.Append(Create(node.Type));
            builder.Append(']');
            return base.Visit(node)!;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            builder.Append('(');
            Visit(node.Left);
            builder.Append('|');
            builder.Append(node.Method?.MetadataToken.ToString(CultureInfo.InvariantCulture) ?? "-");
            builder.Append('|');
            Visit(node.Right);
            builder.Append(')');
            return node;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            builder.Append('(');
            Visit(node.Operand);
            builder.Append(')');
            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            builder.Append("(p");
            builder.Append(parameterOrdinals.TryGetValue(node, out var ordinal)
                ? ordinal.ToString(CultureInfo.InvariantCulture)
                : "?");
            builder.Append(')');
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            builder.Append("(const:");
            builder.Append(Create(node.Type));
            builder.Append(')');
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsCapturedValue(node))
            {
                builder.Append("(captured:");
                builder.Append(Create(node.Type));
                builder.Append(')');
                return node;
            }

            builder.Append("(member:");
            builder.Append(Create(node.Member.DeclaringType ?? typeof(object)));
            builder.Append('.');
            builder.Append(node.Member.Name);
            builder.Append('|');
            Visit(node.Expression);
            builder.Append(')');
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            builder.Append("(call:");
            builder.Append(Create(node.Method.DeclaringType ?? typeof(object)));
            builder.Append('.');
            builder.Append(node.Method.Name);

            if (node.Method.IsGenericMethod)
            {
                builder.Append('<');
                builder.Append(string.Join(",", node.Method.GetGenericArguments().Select(Create)));
                builder.Append('>');
            }

            builder.Append('|');
            Visit(node.Object);
            foreach (var argument in node.Arguments)
            {
                builder.Append('|');
                Visit(argument);
            }

            builder.Append(')');
            return node;
        }

        protected override Expression VisitNew(NewExpression node)
        {
            builder.Append("(new:");
            builder.Append(Create(node.Type));
            builder.Append('|');
            builder.Append(node.Constructor?.MetadataToken.ToString(CultureInfo.InvariantCulture) ?? "-");
            foreach (var argument in node.Arguments)
            {
                builder.Append('|');
                Visit(argument);
            }

            builder.Append(')');
            return node;
        }

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            builder.Append("(init:");
            Visit(node.NewExpression);

            foreach (var binding in node.Bindings.OfType<MemberAssignment>())
            {
                builder.Append('|');
                builder.Append(binding.Member.Name);
                builder.Append('=');
                Visit(binding.Expression);
            }

            builder.Append(')');
            return node;
        }

        protected override Expression VisitNewArray(NewArrayExpression node)
        {
            builder.Append("(array");
            foreach (var expression in node.Expressions)
            {
                builder.Append('|');
                Visit(expression);
            }

            builder.Append(')');
            return node;
        }

        private static bool IsCapturedValue(Expression expression)
        {
            while (expression is MemberExpression member)
            {
                expression = member.Expression!;
            }

            return expression is ConstantExpression;
        }

        public override string ToString() => builder.ToString();
    }
}
