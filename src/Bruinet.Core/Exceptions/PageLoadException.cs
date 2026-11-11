namespace Bruinet.Core.Exceptions;

/// <summary>
/// An address could not be opened: the app did not answer, refused it, or is not the app this head renders. Both Html
/// heads throw it for a navigation that failed, with the address and why (review 01, finding 5).
/// </summary>
public class PageLoadException : BruinetException
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
