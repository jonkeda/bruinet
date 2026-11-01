namespace Bruinet.Core.Locators;

/// <summary>
/// Defines strategies for locating elements in the UI.
/// </summary>
public enum LocatorStrategy
{
    /// <summary>
    /// Locate by AutomationId (MAUI, WPF).
    /// </summary>
    AutomationId,

    /// <summary>
    /// Locate by element ID.
    /// </summary>
    Id,

    /// <summary>
    /// Locate by element name.
    /// </summary>
    Name,

    /// <summary>
    /// Locate by CSS class name.
    /// </summary>
    ClassName,

    /// <summary>
    /// Locate by CSS selector (Blazor).
    /// </summary>
    Css,

    /// <summary>
    /// Locate by XPath expression.
    /// </summary>
    XPath,

    /// <summary>
    /// Locate by visible text content.
    /// </summary>
    Text,

    /// <summary>
    /// Locate by link text (Blazor).
    /// </summary>
    LinkText,

    /// <summary>
    /// Locate by partial link text (Blazor).
    /// </summary>
    PartialLinkText,

    /// <summary>
    /// Locate by HTML tag name.
    /// </summary>
    TagName,

    /// <summary>
    /// Locate by data-testid attribute (Blazor).
    /// </summary>
    DataTestId,

    /// <summary>
    /// Locate by data-automation-id attribute (Blazor).
    /// </summary>
    DataAutomationId,

    /// <summary>
    /// Locate by accessibility ID (MAUI).
    /// </summary>
    AccessibilityId,

    /// <summary>
    /// Locate by control type (WPF).
    /// </summary>
    ControlType,

    /// <summary>
    /// Locate by Control Type and name (FLAUI)
    /// </summary>
    ControlTypeAndName
}
