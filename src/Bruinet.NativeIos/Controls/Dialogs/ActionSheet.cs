using Bruinet.NativeIos.Containers;
using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Text;

namespace Bruinet.NativeIos.Controls.Dialogs;

/// <summary>
/// An action sheet (<c>UIAlertController</c>, style action sheet): its title, its message and its
/// actions by label. Shown over the page, so it does not wait for the page.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in, usually the page it opens over.</typeparam>
public class ActionSheet<TParent> : ContainerObjectBase<TParent, ActionSheet<TParent>>
    where TParent : INativeIosScope<TParent>
{
    // A button only: the title is a text with the same label as a button ("Rename") and came first.
    private const string ButtonType = "XCUIElementTypeButton";

    private static readonly Locator Frame = Locator.ByClassName("XCUIElementTypeSheet");

    /// <summary>The action sheet shown over <paramref name="parent"/>.</summary>
    public ActionSheet(INativeIosScope<TParent> parent)
        : base(parent, Frame)
    {
    }

    /// <inheritdoc />
    public override ActionSheet<TParent> Self => this;

    /// <summary>Shown over its parent: usable while the page behind it is not.</summary>
    protected override bool AsksParent => false;

    /// <summary>The sheet's title: its first text.</summary>
    public Label<ActionSheet<TParent>> Title => new(this, Locator.ByXPath("(.//XCUIElementTypeStaticText)[1]"));

    /// <summary>The sheet's message: its second text.</summary>
    public Label<ActionSheet<TParent>> Message => new(this, Locator.ByXPath("(.//XCUIElementTypeStaticText)[2]"));

    /// <summary>An action, by its label.</summary>
    public Button<ActionSheet<TParent>> Action(string label) => new(this, Locator.ByControlTypeAndName(ButtonType, label));
}
