namespace Bruinet.Core.Exceptions;

/// <summary>
/// Thrown when a page fails to load within the expected time or conditions.
/// </summary>
public class PageLoadException : Exception
{
    /// <summary>
    /// Creates a new PageLoadException with the specified message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public PageLoadException(string message) : base(message) { }

    /// <summary>
    /// Creates a new PageLoadException with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public PageLoadException(string message, Exception innerException)
        : base(message, innerException) { }
}
