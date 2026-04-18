namespace LayerZero.Data;

/// <summary>
/// Represents one parameterized SQL statement.
/// </summary>
/// <param name="CommandText">The SQL command text.</param>
/// <param name="Parameters">The ordered parameters.</param>
public readonly record struct DataSqlStatement(
    string CommandText,
    IReadOnlyList<DataSqlParameter> Parameters);

/// <summary>
/// Represents one SQL parameter value.
/// </summary>
/// <param name="Name">The SQL placeholder token or parameter name.</param>
/// <param name="Value">The parameter value.</param>
public readonly record struct DataSqlParameter(
    string Name,
    object? Value);

internal static class DataSqlParameterToken
{
    private const string Prefix = "__lz_param_";
    private const string Suffix = "__";

    public static string Create(int ordinal)
    {
        return $"{Prefix}{ordinal}{Suffix}";
    }
}
