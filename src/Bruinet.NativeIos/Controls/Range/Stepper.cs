using Bruinet.NativeIos.Containers;
using Bruinet.NativeIos.Controls.Buttons;

namespace Bruinet.NativeIos.Controls.Range;

/// <summary>
/// A stepper (<c>UIStepper</c>): its two buttons. XCUITest does not report a stepper's value; read
/// it from the label the app shows next to it.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
public class Stepper<TParent> : ContainerObjectBase<TParent, Stepper<TParent>>
    where TParent : INativeIosScope<TParent>
{
    /// <summary>The stepper found by <paramref name="locator"/>.</summary>
    public Stepper(INativeIosScope<TParent> parent, Locator locator)
        : base(parent, locator)
    {
    }

    /// <summary>The stepper with accessibility identifier <paramref name="identifier"/>.</summary>
    public Stepper(INativeIosScope<TParent> parent, string identifier)
        : base(parent, identifier)
    {
    }

    /// <inheritdoc />
    public override Stepper<TParent> Self => this;

    /// <summary>The plus button: its first button labelled "Increment".</summary>
    public Button<Stepper<TParent>> Increment => new(this, Locator.ByText("Increment"));

    /// <summary>The minus button: its button labelled "Decrement".</summary>
    public Button<Stepper<TParent>> Decrement => new(this, Locator.ByText("Decrement"));
}
