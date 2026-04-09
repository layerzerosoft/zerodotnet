namespace LayerZero.Testing;

/// <summary>
/// Exception thrown when a LayerZero assertion fails.
/// </summary>
public sealed class AssertionException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="AssertionException"/>.
    /// </summary>
    /// <param name="message">The assertion failure message.</param>
    public AssertionException(string message)
        : base(message)
    {
    }
}
