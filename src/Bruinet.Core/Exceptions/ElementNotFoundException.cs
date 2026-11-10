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

    public ElementNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}
