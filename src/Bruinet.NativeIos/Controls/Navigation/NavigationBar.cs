using Bruinet.NativeIos.Containers;
using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Text;

namespace Bruinet.NativeIos.Controls.Navigation;

/// <summary>
/// A navigation bar (<c>UINavigationBar</c>): its title, its back button and its bar button items.
/// XCUITest names the bar after its title.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
public class NavigationBar<TParent> : ContainerObjectBase<TParent, NavigationBar<TParent>>
    where TParent : INativeIosScope<TParent>
{
    private static readonly Locator Bar = Locator.ByClassName("XCUIElementTypeNavigationBar");

    /// <summary>The page's navigation bar: the first one on the screen.</summary>
    public NavigationBar(INativeIosScope<TParent> parent)
        : base(parent, Bar)
    {
    }

    /// <summary>The navigation bar found by <paramref name="locator"/>.</summary>
    public NavigationBar(INativeIosScope<TParent> parent, Locator locator)
        : base(parent, locator)
    {
    }

    /// <inheritdoc />
    public override NavigationBar<TParent> Self => this;

    /// <summary>The title: the bar's text.</summary>
    public Label<NavigationBar<TParent>> Title => new(this, Locator.ByClassName("XCUIElementTypeStaticText"));

    /// <summary>The back button: XCUITest names it <c>BackButton</c> and labels it with the previous page's title.</summary>
    public Button<NavigationBar<TParent>> BackButton => new(this, Locator.ById("BackButton"));

    /// <summary>A bar button item, by its label.</summary>
    public Button<NavigationBar<TParent>> Action(string label) => new(this, Locator.ByText(label));
}
