using Bruinet.NativeIos.Containers;
using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Text;

namespace Bruinet.NativeIos.Controls.Dialogs;

/// <summary>
/// An alert (<c>UIAlertController</c>, style alert): its title, its message and its buttons by
/// label. Shown over the page, so it does not wait for the page.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in, usually the page it opens over.</typeparam>
public class Alert<TParent> : ContainerObjectBase<TParent, Alert<TParent>>
    where TParent : INativeIosScope<TParent>
{
    private static readonly Locator Frame = Locator.ByClassName("XCUIElementTypeAlert");

    /// <summary>The alert shown over <paramref name="parent"/>.</summary>
    public Alert(INativeIosScope<TParent> parent)
        : base(parent, Frame)
    {
    }

    /// <inheritdoc />
    public override Alert<TParent> Self => this;

    /// <summary>Shown over its parent: usable while the page behind it is not.</summary>
    protected override bool AsksParent => false;

    /// <summary>The alert's title: its first text.</summary>
    public Label<Alert<TParent>> Title => new(this, Locator.ByXPath("(.//XCUIElementTypeStaticText)[1]"));

    /// <summary>The alert's message: its second text.</summary>
    public Label<Alert<TParent>> Message => new(this, Locator.ByXPath("(.//XCUIElementTypeStaticText)[2]"));

    /// <summary>A button, by its label ("OK", "Cancel", "Delete").</summary>
    public Button<Alert<TParent>> Button(string label) => new(this, Locator.ByText(label));

    /// <summary>A text field in the alert, by its position (0-based).</summary>
    public TextField<Alert<TParent>> TextField(int index = 0)
        => new(this, Locator.ByXPath($"(.//XCUIElementTypeTextField | .//XCUIElementTypeSecureTextField)[{index + 1}]"));
}
