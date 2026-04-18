using System.Globalization;

namespace LayerZero.Data.Internal.Sql;

internal static class DataSqlStatementCompiler
{
    public static string RewriteCommandText(
        DataSqlStatement statement,
        Func<int, string> placeholderFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement.CommandText);
        ArgumentNullException.ThrowIfNull(placeholderFactory);

        var commandText = statement.CommandText;
        for (var index = 0; index < statement.Parameters.Count; index++)
        {
            var parameter = statement.Parameters[index];
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                throw new InvalidOperationException(
                    $"LayerZero SQL parameter at ordinal {index.ToString(CultureInfo.InvariantCulture)} does not have a valid placeholder token.");
            }

            commandText = commandText.Replace(parameter.Name, placeholderFactory(index), StringComparison.Ordinal);
        }

        return commandText;
    }
}
