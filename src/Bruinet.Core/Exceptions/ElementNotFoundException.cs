using Bruinet.Core.Locators;

namespace Bruinet.Core.Exceptions;

/// <summary>
/// Exception thrown when an element cannot be found in the UI tree.
/// </summary>
public class ElementNotFoundException : BruinetException
{
    /// <summary>
    /// The locator string representation (for backward compatibility).
    /// </summary>

    /// <summary>
    /// The locator used to search for the element.
    /// </summary>
    public Locator? Locator { get; }

    public ElementNotFoundException(string message) : base(message) { }


    public ElementNotFoundException(Locator locator)
        : base($"Element not found with locator: {locator.Strategy}='{locator.Value}'")
    {
        Locator = locator;
    }

    public ElementNotFoundException(Locator locator, int timeoutMs)
        : base($"Element not found with locator: {locator.Strategy}='{locator.Value}' after {timeoutMs}ms")
    {
        Locator = locator;
    }

    /// <summary>A control's element was not found: <paramref name="message"/> says by which call, where and how long.</summary>
    /// <param name="locator">The locator searched with.</param>
    /// <param name="message">The whole message.</param>
    public ElementNotFoundException(Locator locator, string message)
        : base(message)
    {
        Locator = locator;
    }

    public ElementNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}
