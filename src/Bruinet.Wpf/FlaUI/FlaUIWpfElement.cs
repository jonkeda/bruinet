using FlaUI.Core.AutomationElements;
using Bruinet.FlaUI;

namespace Bruinet.Wpf.FlaUI;

/// <summary>
/// A WPF element through FlaUI's UI Automation: the patterns of <see cref="UiaElementBase"/>, with
/// lookups that answer WPF elements. One attempt each; nothing here waits.
/// </summary>
public sealed class FlaUIWpfElement : UiaElementBase, IWpfElement
{
    /// <summary>The element <paramref name="element"/> of the app <paramref name="app"/>.</summary>
    public FlaUIWpfElement(AutomationElement element, DesktopApp app)
        : base(element, app)
    {
    }

    /// <inheritdoc />
    public IWpfElement? TryFindElement(Locator locator)
        => Live(() => UiaElements.FindFirst(App, Element, locator) is { } found ? new FlaUIWpfElement(found, App) : null);

    /// <inheritdoc />
    public IReadOnlyList<IWpfElement> FindElements(Locator locator)
        => Live<IReadOnlyList<IWpfElement>>(() => [.. UiaElements.FindAll(App, Element, locator).Select(found => new FlaUIWpfElement(found, App))]);
}
