using FlaUI.Core.AutomationElements;
using Bruinet.FlaUI;

namespace Bruinet.WinForms.FlaUI;

/// <summary>
/// A WinForms element through FlaUI's UI Automation: the patterns of <see cref="UiaElementBase"/>, with
/// lookups that answer WinForms elements. One attempt each; nothing here waits.
/// </summary>
internal sealed class FlaUIWinFormsElement : UiaElementBase, IWinFormsElement
{
    /// <summary>The element <paramref name="element"/> of the app <paramref name="app"/>.</summary>
    public FlaUIWinFormsElement(AutomationElement element, DesktopApp app)
        : base(element, app)
    {
    }

    /// <inheritdoc />
    public IWinFormsElement? TryFindElement(Locator locator)
        => Live(() => UiaElements.FindFirst(App, Element, locator) is { } found ? new FlaUIWinFormsElement(found, App) : null);

    /// <inheritdoc />
    public IReadOnlyList<IWinFormsElement> FindElements(Locator locator)
        => Live<IReadOnlyList<IWinFormsElement>>(() => [.. UiaElements.FindAll(App, Element, locator).Select(found => new FlaUIWinFormsElement(found, App))]);
}
