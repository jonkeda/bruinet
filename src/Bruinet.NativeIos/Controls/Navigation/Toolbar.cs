using Bruinet.NativeIos.Containers;
using Bruinet.NativeIos.Controls.Buttons;

namespace Bruinet.NativeIos.Controls.Navigation;

/// <summary>A toolbar (<c>UIToolbar</c>) at the bottom of the page: its bar button items.</summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
public class Toolbar<TParent> : ContainerObjectBase<TParent, Toolbar<TParent>>
    where TParent : INativeIosScope<TParent>
{
    private static readonly Locator Bar = Locator.ByClassName("XCUIElementTypeToolbar");

    /// <summary>The page's toolbar: the first one on the screen.</summary>
    public Toolbar(INativeIosScope<TParent> parent)
        : base(parent, Bar)
    {
    }

    /// <summary>The toolbar found by <paramref name="locator"/>.</summary>
    public Toolbar(INativeIosScope<TParent> parent, Locator locator)
        : base(parent, locator)
    {
    }

    /// <inheritdoc />
    public override Toolbar<TParent> Self => this;

    /// <summary>A bar button item, by its label.</summary>
    public Button<Toolbar<TParent>> Action(string label) => new(this, Locator.ByText(label));
}
