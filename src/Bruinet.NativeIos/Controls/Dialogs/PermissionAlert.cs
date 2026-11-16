using Bruinet.NativeIos.Containers;
using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Text;

namespace Bruinet.NativeIos.Controls.Dialogs;

/// <summary>
/// iOS's privacy prompt (camera, photos, location, notifications), drawn by Springboard over the
/// app. It is not in the app's tree, so it is found in Springboard's (<see cref="INativeIosDriver.FindSystemElements"/>).
/// Its own scope: it never waits for the app. To skip prompts, grant the permission up front with
/// <c>IosHost.Grant</c> or set <c>AutoAcceptAlerts</c>.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in, usually the app root.</typeparam>
public class PermissionAlert<TParent> : ContainerObjectBase<TParent, PermissionAlert<TParent>>
    where TParent : INativeIosScope<TParent>
{
    private static readonly Locator Frame = Locator.ByClassName("XCUIElementTypeAlert");

    /// <summary>The privacy prompt shown over <paramref name="parent"/>.</summary>
    public PermissionAlert(INativeIosScope<TParent> parent)
        : base(parent, Frame)
    {
    }

    /// <inheritdoc />
    public override PermissionAlert<TParent> Self => this;

    /// <summary>The prompt covers the app: whether the app is ready says nothing about the prompt.</summary>
    protected override bool AsksParent => false;

    /// <summary>The prompt, from Springboard's tree.</summary>
    protected override INativeIosElement? TryFindRootElement() => Context.Driver.FindSystemElements(Frame).FirstOrDefault();

    /// <summary>What the app asks for: the prompt's first text.</summary>
    public Label<PermissionAlert<TParent>> Message => new(this, Locator.ByXPath("(.//XCUIElementTypeStaticText)[1]"));

    /// <summary>"Allow", "Allow While Using App", "Allow Full Access" or "OK", whichever the prompt offers first.</summary>
    public Button<PermissionAlert<TParent>> Allow
        => new(this, Locator.ByXPath("(.//XCUIElementTypeButton[starts-with(@label, 'Allow') or @label = 'OK'])[1]"));

    /// <summary>"Allow Once".</summary>
    public Button<PermissionAlert<TParent>> AllowOnce => new(this, Locator.ByText("Allow Once"));

    /// <summary>"Don't Allow" (iOS writes it with a typographic apostrophe).</summary>
    public Button<PermissionAlert<TParent>> Deny
        => new(this, Locator.ByXPath(".//XCUIElementTypeButton[starts-with(@label, 'Don') and contains(@label, 'Allow')]"));
}
