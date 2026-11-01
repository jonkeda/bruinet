namespace Bruinet.Core.Locators;

/// <summary>
/// Immutable value object representing a locator for finding UI elements.
/// </summary>
public sealed class Locator
{
    /// <summary>
    /// The strategy used to locate the element.
    /// </summary>
    public LocatorStrategy Strategy { get; }

    /// <summary>
    /// The value used with the strategy (e.g., the AutomationId string).
    /// </summary>
    public string Value { get; }

    public string? TypeName { get; }

    /// <summary>
    /// Optional parent locator for scoped searches.
    /// </summary>
    public Locator? Parent { get; }

    /// <summary>
    /// Creates a new Locator with the specified strategy and value.
    /// </summary>
    public Locator(LocatorStrategy strategy, string value, Locator? parent = null)
    {
        Strategy = strategy;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Parent = parent;
    }

    public Locator(string typeName, string value, Locator? parent = null)
    {
        Strategy = LocatorStrategy.ControlTypeAndName;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        TypeName = typeName;
        Parent = parent;
    }

    /// <summary>
    /// Create a locator by AutomationId (MAUI, WPF).
    /// </summary>
    public static Locator ByAutomationId(string automationId)
        => new(LocatorStrategy.AutomationId, automationId);

    /// <summary>
    /// Create a locator by element ID.
    /// </summary>
    public static Locator ById(string id)
        => new(LocatorStrategy.Id, id);

    /// <summary>
    /// Create a locator by element name.
    /// </summary>
    public static Locator ByName(string name)
        => new(LocatorStrategy.Name, name);

    /// <summary>
    /// Create a locator by CSS class name.
    /// </summary>
    public static Locator ByClassName(string className)
        => new(LocatorStrategy.ClassName, className);

    /// <summary>
    /// Create a locator by CSS selector (Blazor).
    /// </summary>
    public static Locator ByCss(string cssSelector)
        => new(LocatorStrategy.Css, cssSelector);

    /// <summary>
    /// Create a locator by XPath expression.
    /// </summary>
    public static Locator ByXPath(string xpath)
        => new(LocatorStrategy.XPath, xpath);

    /// <summary>
    /// Create a locator by visible text content.
    /// </summary>
    public static Locator ByText(string text)
        => new(LocatorStrategy.Text, text);

    /// <summary>
    /// Create a locator by link text (Blazor).
    /// </summary>
    public static Locator ByLinkText(string linkText)
        => new(LocatorStrategy.LinkText, linkText);

    /// <summary>
    /// Create a locator by partial link text (Blazor).
    /// </summary>
    public static Locator ByPartialLinkText(string partialLinkText)
        => new(LocatorStrategy.PartialLinkText, partialLinkText);

    /// <summary>
    /// Create a locator by HTML tag name.
    /// </summary>
    public static Locator ByTagName(string tagName)
        => new(LocatorStrategy.TagName, tagName);

    /// <summary>
    /// Create a locator by data-testid attribute (Blazor).
    /// </summary>
    public static Locator ByDataTestId(string testId)
        => new(LocatorStrategy.DataTestId, testId);

    /// <summary>
    /// Create a locator by accessibility ID (MAUI).
    /// </summary>
    public static Locator ByAccessibilityId(string accessibilityId)
        => new(LocatorStrategy.AccessibilityId, accessibilityId);

    /// <summary>
    /// Create a locator by control type (WPF).
    /// </summary>
    public static Locator ByControlType(string controlType)
        => new(LocatorStrategy.ControlType, controlType);

    /// <summary>
    /// Create a new locator scoped to a parent element.
    /// </summary>
    public Locator ScopedTo(Locator parent)
        => new(Strategy, Value, parent);

    /// <summary>
    /// Create a locator with a different strategy but same value.
    /// </summary>
    public Locator WithStrategy(LocatorStrategy strategy)
        => new(strategy, Value, Parent);

    public override string ToString()
        => Parent != null
            ? $"{Parent} > {Strategy}:{Value}"
            : $"{Strategy}:{Value}";

    public override bool Equals(object? obj)
    {
        if (obj is not Locator other)
            return false;
        return Strategy == other.Strategy
               && Value == other.Value
               && Equals(Parent, other.Parent);
    }

    public override int GetHashCode()
        => HashCode.Combine(Strategy, Value, Parent);

    public static bool operator ==(Locator? left, Locator? right)
        => Equals(left, right);

    public static bool operator !=(Locator? left, Locator? right)
        => !Equals(left, right);
}
