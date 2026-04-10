using System.Text;
using LayerZero.Core;
using LayerZero.Validation;

namespace LayerZero.Testing;

internal static class AssertionFormatter
{
    public static string FormatErrors(IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var error in errors)
        {
            builder.AppendLine().Append(" - ").Append(error);
        }

        return builder.ToString();
    }

    public static string FormatValidationFailures(IReadOnlyList<ValidationFailure> failures)
    {
        if (failures.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var failure in failures)
        {
            builder
                .AppendLine()
                .Append(" - ")
                .Append(failure.Code)
                .Append(" (")
                .Append(failure.PropertyName)
                .Append("): ")
                .Append(failure.Message);
        }

        return builder.ToString();
    }
}
