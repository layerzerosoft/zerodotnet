using System.Globalization;

namespace LayerZero.Validation;

/// <summary>
/// Builds validation rules for one property.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
/// <typeparam name="TProperty">The property type.</typeparam>
public sealed class RuleBuilder<T, TProperty>
{
    private readonly Action<Rule<T>> addRule;
    private readonly Func<T, TProperty> accessor;
    private readonly string propertyName;

    internal RuleBuilder(string propertyName, Func<T, TProperty> accessor, Action<Rule<T>> addRule)
    {
        this.propertyName = propertyName;
        this.accessor = accessor;
        this.addRule = addRule;
    }

    /// <summary>
    /// Adds a rule requiring the property value to be non-null.
    /// </summary>
    /// <param name="message">Optional custom failure message.</param>
    /// <param name="code">Optional custom failure code.</param>
    /// <returns>The current rule builder.</returns>
    public RuleBuilder<T, TProperty> NotNull(string? message = null, string code = ValidationCodes.NotNull)
    {
        return AddSyncRule(
            value => value is not null,
            code,
            message ?? $"{propertyName} must not be null.");
    }

    /// <summary>
    /// Adds a rule requiring the property value to be non-empty.
    /// </summary>
    /// <param name="message">Optional custom failure message.</param>
    /// <param name="code">Optional custom failure code.</param>
    /// <returns>The current rule builder.</returns>
    public RuleBuilder<T, TProperty> NotEmpty(string? message = null, string code = ValidationCodes.NotEmpty)
    {
        return AddSyncRule(
            HasValue,
            code,
            message ?? $"{propertyName} must not be empty.");
    }

    /// <summary>
    /// Adds a rule requiring the string representation to be no longer than the configured maximum.
    /// </summary>
    /// <param name="maximum">The maximum allowed length.</param>
    /// <param name="message">Optional custom failure message.</param>
    /// <param name="code">Optional custom failure code.</param>
    /// <returns>The current rule builder.</returns>
    public RuleBuilder<T, TProperty> MaximumLength(
        int maximum,
        string? message = null,
        string code = ValidationCodes.MaximumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);

        return AddSyncRule(
            value => GetLength(value) <= maximum,
            code,
            message ?? $"{propertyName} must be {maximum} characters or fewer.");
    }

    /// <summary>
    /// Adds a rule requiring the string representation to be at least the configured minimum.
    /// </summary>
    /// <param name="minimum">The minimum allowed length.</param>
    /// <param name="message">Optional custom failure message.</param>
    /// <param name="code">Optional custom failure code.</param>
    /// <returns>The current rule builder.</returns>
    public RuleBuilder<T, TProperty> MinimumLength(
        int minimum,
        string? message = null,
        string code = ValidationCodes.MinimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);

        return AddSyncRule(
            value => value is null || GetLength(value) >= minimum,
            code,
            message ?? $"{propertyName} must be at least {minimum} characters.");
    }

    /// <summary>
    /// Adds a custom synchronous predicate rule.
    /// </summary>
    /// <param name="predicate">The predicate that must return true.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="code">Optional custom failure code.</param>
    /// <returns>The current rule builder.</returns>
    public RuleBuilder<T, TProperty> Must(
        Func<TProperty, bool> predicate,
        string message,
        string code = ValidationCodes.Must)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return AddSyncRule(predicate, code, message);
    }

    /// <summary>
    /// Adds a custom asynchronous predicate rule.
    /// </summary>
    /// <param name="predicate">The predicate that must return true.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="code">Optional custom failure code.</param>
    /// <returns>The current rule builder.</returns>
    public RuleBuilder<T, TProperty> MustAsync(
        Func<TProperty, CancellationToken, ValueTask<bool>> predicate,
        string message,
        string code = ValidationCodes.Must)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        addRule(new Rule<T>(async (instance, _, cancellationToken) =>
        {
            TProperty value = accessor(instance);
            bool isValid = await predicate(value, cancellationToken).ConfigureAwait(false);
            return isValid ? null : new ValidationFailure(propertyName, code, message, value);
        }));

        return this;
    }

    private RuleBuilder<T, TProperty> AddSyncRule(
        Func<TProperty, bool> predicate,
        string code,
        string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        addRule(new Rule<T>((instance, _, _) =>
        {
            TProperty value = accessor(instance);
            return ValueTask.FromResult(predicate(value)
                ? null
                : new ValidationFailure(propertyName, code, message, value));
        }));

        return this;
    }

    private static bool HasValue(TProperty value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return !string.IsNullOrWhiteSpace(text);
        }

        if (value is Guid guid)
        {
            return guid != Guid.Empty;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            System.Collections.IEnumerator enumerator = enumerable.GetEnumerator();
            try
            {
                return enumerator.MoveNext();
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        return !EqualityComparer<TProperty>.Default.Equals(value, default!);
    }

    private static int GetLength(TProperty value)
    {
        if (value is null)
        {
            return 0;
        }

        return value is string text
            ? text.Length
            : Convert.ToString(value, CultureInfo.InvariantCulture)?.Length ?? 0;
    }
}
