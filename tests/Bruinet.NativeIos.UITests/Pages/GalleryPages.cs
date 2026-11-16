using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Collection;
using Bruinet.NativeIos.Controls.Container;
using Bruinet.NativeIos.Controls.Dialogs;
using Bruinet.NativeIos.Controls.Display;
using Bruinet.NativeIos.Controls.Navigation;
using Bruinet.NativeIos.Controls.Range;
using Bruinet.NativeIos.Controls.Selection;
using Bruinet.NativeIos.Controls.Text;
using Bruinet.NativeIos.Controls.Toggle;

namespace Bruinet.NativeIos.UITests.Pages;

// The page and control names match the native Android gallery's, so the shared features run on both.

/// <summary>The sign-in screen.</summary>
[TestPage("Sign In")]
public sealed class LoginPage(INativeIosTestContext context) : PageObjectBase<LoginPage>(context)
{
    protected override Locator? ReadyLocator => Locator.ById("login");

    public TextField<LoginPage> UserName => new(this, "username");

    public SecureTextField<LoginPage> Password => new(this, "password");

    public Button<LoginPage> SignIn => new(this, "login");

    public Label<LoginPage> Status => new(this, "status");

    public NavigationBar<LoginPage> Bar => new(this);
}

/// <summary>One of each input control; the status line says what changed last.</summary>
[TestPage("Controls")]
public sealed class ControlsPage(INativeIosTestContext context) : PageObjectBase<ControlsPage>(context)
{
    protected override Locator? ReadyLocator => Locator.ById("status");

    public Label<ControlsPage> Status => new(this, "status");

    public Switch<ControlsPage> Agree => new(this, "agree");

    public Switch<ControlsPage> Notifications => new(this, "notifications");

    public SegmentedControl<ControlsPage> Size => new(this, "size");

    public PickerWheel<ControlsPage> Color => new(this, "color");

    public Slider<ControlsPage> Volume => new(this, "volume", maximum: 100);

    public Stepper<ControlsPage> Quantity => new(this, "quantity");

    public ProgressView<ControlsPage> Progress => new(this, "progress");

    public TextField<ControlsPage> Notes => new(this, "notes");

    public TextView<ControlsPage> Comments => new(this, "comments");

    public Image<ControlsPage> Logo => new(this, "logo");

    public ScrollView<ControlsPage> Scroll => new(this, "scroll");

    public Button<ControlsPage> BottomButton => new(this, "bottom_button");
}

/// <summary>200 invoices in a table view, with pull to refresh.</summary>
[TestPage("Invoices")]
public sealed class InvoicesPage(INativeIosTestContext context) : PageObjectBase<InvoicesPage>(context)
{
    protected override Locator? ReadyLocator => Locator.ById("invoices");

    public Label<InvoicesPage> Count => new(this, "count");

    public TableView<InvoicesPage> Invoices => new(this, "invoices", itemKeyId: "name");

    public ScrollView<InvoicesPage> Refresh => new(this, "invoices");
}

/// <summary>An alert, an alert with a text field, an action sheet, the camera permission prompt, and a long piece of work.</summary>
[TestPage("Dialogs")]
public sealed class DialogsPage(INativeIosTestContext context) : PageObjectBase<DialogsPage>(context)
{
    protected override Locator? ReadyLocator => Locator.ById("status");

    public Button<DialogsPage> ShowAlert => new(this, "show_alert");

    public Button<DialogsPage> Rename => new(this, "rename");

    public Button<DialogsPage> ShowSheet => new(this, "show_sheet");

    public Button<DialogsPage> AskCamera => new(this, "ask_camera");

    public Button<DialogsPage> Work => new(this, "work");

    public Label<DialogsPage> Status => new(this, "status");

    public Alert<DialogsPage> Alert => new(this);

    public ActionSheet<DialogsPage> Sheet => new(this);

    public PermissionAlert<DialogsPage> Permission => new(this);
}

/// <summary>A segmented control as tabs, a tab bar and navigation bar buttons.</summary>
[TestPage("Navigation")]
public sealed class NavigationPage(INativeIosTestContext context) : PageObjectBase<NavigationPage>(context)
{
    protected override Locator? ReadyLocator => Locator.ById("tabs");

    public NavigationBar<NavigationPage> Bar => new(this);

    public SegmentedControl<NavigationPage> Tabs => new(this, "tabs");

    public TabBar<NavigationPage> Sections => new(this, "bottom_nav");

    public Label<NavigationPage> Status => new(this, "status");
}
