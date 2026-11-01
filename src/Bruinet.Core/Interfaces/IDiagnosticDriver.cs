namespace Bruinet.Core.Interfaces;

/// <summary>
/// Optional diagnostic interface for debugging and troubleshooting.
/// Not all drivers need to implement this - it is optional.
/// Provides access to page source and automation tree for debugging failed tests.
/// </summary>
public interface IDiagnosticDriver
{
    /// <summary>
    /// Gets the page/window source (XML, HTML, or native format).
    /// For Appium, this returns the XML page source.
    /// For Blazor/Playwright, this returns HTML.
    /// For FlaUI, this returns a serialized automation tree.
    /// </summary>
    /// <returns>The page source as a string.</returns>
    string GetPageSource();

    /// <summary>
    /// Gets a human-readable text representation of the automation tree.
    /// Useful for debugging element hierarchy and understanding UI structure.
    /// </summary>
    /// <returns>A formatted string showing the automation tree.</returns>
    string GetAutomationTree();
}
