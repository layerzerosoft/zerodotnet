using System.Linq.Expressions;
using System.Reflection;

namespace LayerZero.Data.Internal;

internal static class ExpressionHelpers
{
    public static string GetPropertyName(LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var body = expression.Body switch
        {
            UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary => unary.Operand,
            _ => expression.Body,
        };

        if (body is not MemberExpression member || member.Member.MemberType != System.Reflection.MemberTypes.Property)
        {
            throw new InvalidOperationException("Only simple property expressions are supported.");
        }

        return member.Member.Name;
    }

    public static PropertyInfo GetProperty(LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var body = expression.Body switch
        {
            UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary => unary.Operand,
            _ => expression.Body,
        };

        if (body is not MemberExpression member || member.Member is not PropertyInfo property)
        {
            throw new InvalidOperationException("Only simple property expressions are supported.");
        }

        return property;
    }

    public static string ToSnakeLikeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var builder = new System.Text.StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(name[index - 1]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
