namespace Bruinet.Samples.NativeIos.App.Screens;

/// <summary>The gallery's start screen: a button per screen.</summary>
public sealed class GalleryScreen() : Screen("Gallery")
{
    /// <summary>The deep link scheme: <c>bruinet-native://login</c> opens the sign-in screen.</summary>
    public const string Scheme = "bruinet-native";

    /// <summary>The screen a deep link names, or null for a link to no screen.</summary>
    public static Screen? ForLink(NSUrl url)
    {
        if (!string.Equals(url.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = new NSUrlComponents(url, false).QueryItems?
            .Where(item => item.Value is not null)
            .ToDictionary(item => item.Name, item => item.Value!) ?? [];
        return url.Host switch
        {
            "login" => new LoginScreen(query),
            "controls" => new ControlsScreen(),
            "invoices" => new InvoicesScreen(),
            "dialogs" => new DialogsScreen(),
            "navigation" => new NavigationScreen(),
            _ => null,
        };
    }

    /// <inheritdoc />
    protected override void Build()
    {
        base.Build();
        Stack.AddArrangedSubview(Button("open_login", "Login", () => Push(new LoginScreen())));
        Stack.AddArrangedSubview(Button("open_controls", "Controls", () => Push(new ControlsScreen())));
        Stack.AddArrangedSubview(Button("open_list", "Invoices", () => Push(new InvoicesScreen())));
        Stack.AddArrangedSubview(Button("open_dialogs", "Dialogs", () => Push(new DialogsScreen())));
        Stack.AddArrangedSubview(Button("open_navigation", "Navigation", () => Push(new NavigationScreen())));
    }

    private void Push(Screen screen) => NavigationController!.PushViewController(screen, true);
}
