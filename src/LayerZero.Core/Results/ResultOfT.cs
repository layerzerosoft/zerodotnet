namespace LayerZero.Core;

/// <summary>
/// Represents a success or a failure with a return value.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public sealed class Result<T>
{
    private readonly T? value;

    private Result(T? value, bool isSuccess, Error[] errors)
    {
        this.value = value;
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
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>
    /// Gets the success value, or throws when the result is a failure.
    /// </summary>
    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    /// <summary>
    /// Gets the success value when available.
    /// </summary>
    public T? ValueOrDefault => IsSuccess ? value : default;

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful result.</returns>
    public static Result<T> Success(T value) => new(value, isSuccess: true, []);

    /// <summary>
    /// Creates a failed result with one error.
    /// </summary>
    /// <param name="error">The failure error.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, isSuccess: false, [error]);
    }

    /// <summary>
    /// Creates a failed result with one or more errors.
    /// </summary>
    /// <param name="errors">The failure errors.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Failure(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        Error[] materialized = errors.Where(error => error is not null).ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("A failed result must contain at least one error.", nameof(errors));
        }

        return new Result<T>(default, isSuccess: false, materialized);
    }

    /// <summary>
    /// Converts this result to an untyped result.
    /// </summary>
    /// <returns>A result with the same success or failure state.</returns>
    public Result ToResult() => IsSuccess ? Result.Success() : Result.Failure(Errors);
}
