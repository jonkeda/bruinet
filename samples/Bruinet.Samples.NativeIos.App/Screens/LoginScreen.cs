namespace Bruinet.Samples.NativeIos.App.Screens;

/// <summary>
/// Sign in: the button enables once both fields are filled; signing in is busy for a moment,
/// then greets the user. <c>bruinet-native://login?stuck=1</c> keeps the button disabled forever,
/// the app bug the R0 proof needs.
/// </summary>
public sealed class LoginScreen(IReadOnlyDictionary<string, string>? query = null) : Screen("Sign in", query)
{
    /// <inheritdoc />
    protected override void Build()
    {
        base.Build();
        var username = new UITextField { Placeholder = "User name", AccessibilityIdentifier = "username", BorderStyle = UITextBorderStyle.RoundedRect, AutocorrectionType = UITextAutocorrectionType.No, AutocapitalizationType = UITextAutocapitalizationType.None };
        var password = new UITextField { Placeholder = "Password", AccessibilityIdentifier = "password", BorderStyle = UITextBorderStyle.RoundedRect, SecureTextEntry = true };

        // A one-time-code field is not a credential: AutoFill offers no "Save Password?" sheet, which
        // would cover the next screen the tests open.
        username.TextContentType = UITextContentType.OneTimeCode;
        password.TextContentType = UITextContentType.OneTimeCode;
        var status = Label("status", "Signed out");
        var stuck = Query.TryGetValue("stuck", out var value) && value == "1";
        UIButton login = null!;
        login = Button("login", "Sign in", () =>
        {
            status.Text = "Signing in";
            Busy(1500, () => status.Text = $"Welcome, {username.Text}");
        });
        login.Enabled = false;

        void Update() => login.Enabled = !stuck && username.Text?.Length > 0 && password.Text?.Length > 0;
        username.EditingChanged += (_, _) => Update();
        password.EditingChanged += (_, _) => Update();

        Stack.AddArrangedSubview(username);
        Stack.AddArrangedSubview(password);
        Stack.AddArrangedSubview(login);
        Stack.AddArrangedSubview(status);
    }
}
