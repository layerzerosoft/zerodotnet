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
/// <param name="Name">The parameter name.</param>
/// <param name="Value">The parameter value.</param>
public readonly record struct DataSqlParameter(
    string Name,
    object? Value);
