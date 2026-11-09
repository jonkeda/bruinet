namespace Bruinet.Wpf.UITests.PageObjects;

/// <summary>
/// Page object for the main shell window with its navigation sidebar. The window itself is the
/// lookup root, so the shell is known by its title in the sidebar: a WPF panel has no automation
/// peer, so the sidebar itself is not in the tree.
/// </summary>
public class ShellPage : PageObjectBase<ShellPage>
{
    public Label<ShellPage> AppTitleText => Label("AppTitleText");
    public Button<ShellPage> NavHomeButton => Button("NavHomeButton");
    public Button<ShellPage> NavLoginButton => Button("NavLoginButton");
    public Button<ShellPage> NavFormsButton => Button("NavFormsButton");
    public Button<ShellPage> NavDataGridButton => Button("NavDataGridButton");

    public ShellPage(IWpfTestContext context) : base(context) { }

    /// <inheritdoc />
    protected override Locator ReadyLocator => Locator.ByAutomationId("AppTitleText");

    /// <summary>Navigate to the Home page via sidebar button.</summary>
    public HomePage NavigateToHome()
    {
        NavHomeButton.Click();
        var page = new HomePage(Context);
        page.AssertLoaded(true);
        return page;
    }

    /// <summary>Navigate to the Login page via sidebar button.</summary>
    public LoginPage NavigateToLogin()
    {
        NavLoginButton.Click();
        var page = new LoginPage(Context);
        page.AssertLoaded(true);
        return page;
    }
}
