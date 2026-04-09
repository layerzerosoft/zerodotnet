namespace LayerZero.ZeroDotNet;

/// <summary>
/// Represents a success or a failure without a return value.
/// </summary>
public sealed class ZeroResult
{
    private static readonly ZeroError[] NoErrors = [];

    private ZeroResult(bool isSuccess, ZeroError[] errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    /// <summary>
    /// Gets whether the operation completed successfully.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the operation errors when the result is a failure.
    /// </summary>
    public IReadOnlyList<ZeroError> Errors { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static ZeroResult Success() => new(isSuccess: true, NoErrors);

    /// <summary>
    /// Creates a failed result with one error.
    /// </summary>
    /// <param name="error">The failure error.</param>
    /// <returns>A failed result.</returns>
    public static ZeroResult Failure(ZeroError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ZeroResult(isSuccess: false, [error]);
    }

    /// <summary>
    /// Creates a failed result with one or more errors.
    /// </summary>
    /// <param name="errors">The failure errors.</param>
    /// <returns>A failed result.</returns>
    public static ZeroResult Failure(IEnumerable<ZeroError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        ZeroError[] materialized = errors.Where(error => error is not null).ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("A failed result must contain at least one error.", nameof(errors));
        }

        return new ZeroResult(isSuccess: false, materialized);
    }
}
