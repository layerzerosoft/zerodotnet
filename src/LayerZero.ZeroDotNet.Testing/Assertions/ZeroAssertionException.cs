namespace LayerZero.ZeroDotNet.Testing;

/// <summary>
/// Exception thrown when a ZeroDotNet assertion fails.
/// </summary>
public sealed class ZeroAssertionException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="ZeroAssertionException"/>.
    /// </summary>
    /// <param name="message">The assertion failure message.</param>
    public ZeroAssertionException(string message)
        : base(message)
    {
    }
}
