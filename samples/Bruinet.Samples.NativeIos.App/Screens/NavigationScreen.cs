namespace Bruinet.Samples.NativeIos.App.Screens;

/// <summary>Segments, a tab bar and navigation bar actions; the status says where the user is.</summary>
public sealed class NavigationScreen() : Screen("Navigation")
{
    private string _tab = "One";
    private string _section = "Home";
    private UILabel _status = null!;

    /// <inheritdoc />
    protected override void Build()
    {
        base.Build();
        _status = Label("status", "Tab One, Home");

        var tabs = new UISegmentedControl("One", "Two", "Three") { AccessibilityIdentifier = "tabs", SelectedSegment = 0 };
        tabs.ValueChanged += (_, _) =>
        {
            _tab = tabs.TitleAt(tabs.SelectedSegment)!;
            Show();
        };
        Stack.AddArrangedSubview(tabs);
        Stack.AddArrangedSubview(_status);

        var items = new[] { ("Home", "house"), ("Search", "magnifyingglass"), ("Profile", "person") }
            .Select((item, index) => new UITabBarItem(item.Item1, UIImage.GetSystemImage(item.Item2), index))
            .ToArray();
        var bottom = new UITabBar { AccessibilityIdentifier = "bottom_nav", Items = items, SelectedItem = items[0], TranslatesAutoresizingMaskIntoConstraints = false };
        bottom.ItemSelected += (_, e) =>
        {
            _section = e.Item.Title!;
            Show();
        };
        View!.AddSubview(bottom);
        bottom.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor).Active = true;
        bottom.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor).Active = true;
        bottom.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor).Active = true;

        NavigationItem.RightBarButtonItems =
        [
            new UIBarButtonItem("About", UIBarButtonItemStyle.Plain, (_, _) => _status.Text = "About chosen"),
            new UIBarButtonItem("Refresh", UIBarButtonItemStyle.Plain, (_, _) => _status.Text = "Refresh chosen"),
        ];
    }

    private void Show() => _status.Text = $"Tab {_tab}, {_section}";
}
