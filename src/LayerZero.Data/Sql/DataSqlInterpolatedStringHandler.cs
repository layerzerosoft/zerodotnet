using System.Runtime.CompilerServices;
using System.Text;

namespace LayerZero.Data;

/// <summary>
/// Builds one parameterized SQL statement from an interpolated string.
/// </summary>
[InterpolatedStringHandler]
public ref struct DataSqlInterpolatedStringHandler
{
    private readonly StringBuilder builder;
    private List<DataSqlParameter>? parameters;

    /// <summary>
    /// Initializes a new handler.
    /// </summary>
    /// <param name="literalLength">The total literal length.</param>
    /// <param name="formattedCount">The number of formatted values.</param>
    public DataSqlInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        builder = new StringBuilder(literalLength + (formattedCount * 4));
        parameters = formattedCount == 0 ? null : new List<DataSqlParameter>(formattedCount);
    }

    /// <summary>
    /// Appends one SQL literal segment.
    /// </summary>
    /// <param name="value">The literal value.</param>
    public void AppendLiteral(string value) => builder.Append(value);

    /// <summary>
    /// Appends one formatted parameter value.
    /// </summary>
    /// <typeparam name="TValue">The parameter type.</typeparam>
    /// <param name="value">The parameter value.</param>
    public void AppendFormatted<TValue>(TValue value)
    {
        parameters ??= [];
        var name = $"@p{parameters.Count}";
        parameters.Add(new DataSqlParameter(name, value));
        builder.Append(name);
    }

    /// <summary>
    /// Appends one formatted parameter value.
    /// </summary>
    /// <typeparam name="TValue">The parameter type.</typeparam>
    /// <param name="value">The parameter value.</param>
    /// <param name="format">The ignored format string.</param>
    public void AppendFormatted<TValue>(TValue value, string? format) => AppendFormatted(value);

    /// <summary>
    /// Appends one formatted parameter value.
    /// </summary>
    /// <typeparam name="TValue">The parameter type.</typeparam>
    /// <param name="value">The parameter value.</param>
    /// <param name="alignment">The ignored alignment.</param>
    public void AppendFormatted<TValue>(TValue value, int alignment) => AppendFormatted(value);

    /// <summary>
    /// Appends one formatted parameter value.
    /// </summary>
    /// <typeparam name="TValue">The parameter type.</typeparam>
    /// <param name="value">The parameter value.</param>
    /// <param name="alignment">The ignored alignment.</param>
    /// <param name="format">The ignored format string.</param>
    public void AppendFormatted<TValue>(TValue value, int alignment, string? format) => AppendFormatted(value);

    /// <summary>
    /// Builds the SQL statement.
    /// </summary>
    /// <returns>The parameterized statement.</returns>
    public DataSqlStatement Build() => new(builder.ToString(), parameters ?? []);
}
