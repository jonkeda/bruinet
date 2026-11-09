namespace Bruinet.Wpf.UITests.PageObjects;

/// <summary>
/// Page object for the Login page with form validation and busy state handling. The page is busy
/// while its busy indicator is shown, so its waits (<c>WaitReady</c>, <c>WaitBusy</c>) read it.
/// </summary>
public class LoginPage : PageObjectBase<LoginPage>
{
    public Label<LoginPage> LoginHeader => Label("LoginHeader");
    public TextBox<LoginPage> UsernameTextBox => TextBox("UsernameTextBox");
    public Label<LoginPage> UsernameErrorText => Label("UsernameErrorText");
    public PasswordBox<LoginPage> PasswordField => PasswordBox("PasswordBox");
    public Label<LoginPage> PasswordErrorText => Label("PasswordErrorText");
    public Label<LoginPage> LoginErrorText => Label("LoginErrorText");
    public Button<LoginPage> LoginButton => Button("LoginButton");
    public Button<LoginPage> CancelButton => Button("CancelButton");
    public Label<LoginPage> BusyIndicator => Label("BusyIndicator");
    public Label<LoginPage> SuccessText => Label("SuccessText");

    public LoginPage(IWpfTestContext context) : base(context) { }

    private static readonly Locator BusyIndicatorLocator = Locator.ByAutomationId("BusyIndicator");

    /// <inheritdoc />
    protected override IReadOnlyList<Locator> BusyIndicators => [BusyIndicatorLocator];

    #region Busy State

    /// <summary>
    /// Waits until the busy indicator is hidden or gone, as one call on the app's window: a valid
    /// login leaves this page, and the indicator with it.
    /// </summary>
    public bool WaitNotBusy(int? timeoutMs = null)
        => Context.WaitFirst(timeoutMs, () => Context.TryFindElement(BusyIndicatorLocator)?.Visible != true) >= 0;

    /// <summary>Waits until the page is loaded and not busy.</summary>
    public bool WaitPageReady(int? timeoutMs = null) => WaitReady(timeoutMs);

    #endregion

    #region Form Actions

    /// <summary>Enter username and password.</summary>
    public LoginPage EnterCredentials(string username, string password)
    {
        UsernameTextBox.SetText(username);
        PasswordField.SetText(password);
        return this;
    }

    /// <summary>Click the Sign In button.</summary>
    public LoginPage ClickLogin()
    {
        LoginButton.Click();
        return this;
    }

    /// <summary>Click the Cancel button.</summary>
    public LoginPage ClickCancel()
    {
        CancelButton.Click();
        return this;
    }

    /// <summary>Submit valid credentials and wait for navigation to home.</summary>
    public HomePage SubmitValidLogin(string username, string password)
    {
        EnterCredentials(username, password);
        ClickLogin();

        var homePage = new HomePage(Context);
        homePage.AssertLoaded(true);
        return homePage;
    }

    /// <summary>Submit invalid credentials and wait for busy to finish.</summary>
    public LoginPage SubmitInvalidLogin(string username, string password)
    {
        EnterCredentials(username, password);
        ClickLogin();
        WaitNotBusy();
        return this;
    }

    #endregion
}
