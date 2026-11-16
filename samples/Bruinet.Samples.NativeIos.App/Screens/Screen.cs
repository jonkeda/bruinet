namespace Bruinet.Samples.NativeIos.App.Screens;

/// <summary>
/// A gallery screen: its title, a vertical stack for its views, and the busy overlay Bruinet reads
/// as the busy signal (accessibility identifier <c>bruinet_busy</c>), shown while the screen works.
/// </summary>
public abstract class Screen : UIViewController
{
    private readonly string _title;
    private UIView _busy = null!;

    /// <summary>Opens a screen with the deep link's query.</summary>
    /// <param name="title">The navigation bar title, which a test reads as the page title.</param>
    /// <param name="query">The deep link's query parameters.</param>
    protected Screen(string title, IReadOnlyDictionary<string, string>? query = null)
    {
        _title = title;
        Query = query ?? new Dictionary<string, string>();
    }

    /// <summary>The query parameters of the deep link that opened the screen.</summary>
    protected IReadOnlyDictionary<string, string> Query { get; }

    /// <summary>The screen's views, top to bottom, below the navigation bar.</summary>
    protected UIStackView Stack { get; } = new() { Axis = UILayoutConstraintAxis.Vertical, Spacing = 12, TranslatesAutoresizingMaskIntoConstraints = false };

    /// <inheritdoc />
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = _title;
        View!.BackgroundColor = UIColor.SystemBackground;
        Build();

        _busy = new UIView { BackgroundColor = UIColor.Black.ColorWithAlpha(0.33f), Hidden = true, TranslatesAutoresizingMaskIntoConstraints = false };
        _busy.IsAccessibilityElement = true;
        _busy.AccessibilityIdentifier = "bruinet_busy";
        _busy.AccessibilityLabel = "Busy";
        var spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large) { TranslatesAutoresizingMaskIntoConstraints = false };
        spinner.StartAnimating();
        _busy.AddSubview(spinner);
        View.AddSubview(_busy);
        Pin(_busy, View);
        spinner.CenterXAnchor.ConstraintEqualTo(_busy.CenterXAnchor).Active = true;
        spinner.CenterYAnchor.ConstraintEqualTo(_busy.CenterYAnchor).Active = true;
    }

    /// <summary>Adds the screen's views; by default to <see cref="Stack"/>, laid out below the navigation bar.</summary>
    protected virtual void Build()
    {
        View!.AddSubview(Stack);
        var guide = View.SafeAreaLayoutGuide;
        Stack.TopAnchor.ConstraintEqualTo(guide.TopAnchor, 16).Active = true;
        Stack.LeadingAnchor.ConstraintEqualTo(guide.LeadingAnchor, 16).Active = true;
        Stack.TrailingAnchor.ConstraintEqualTo(guide.TrailingAnchor, -16).Active = true;
    }

    /// <summary>Shows the busy overlay for <paramref name="milliseconds"/>, then runs <paramref name="done"/>.</summary>
    protected void Busy(int milliseconds, Action done)
    {
        _busy.Hidden = false;
        View!.BringSubviewToFront(_busy);
        NSTimer.CreateScheduledTimer(TimeSpan.FromMilliseconds(milliseconds), _ =>
        {
            _busy.Hidden = true;
            done();
        });
    }

    /// <summary>A system button with an identifier, that runs <paramref name="tapped"/>.</summary>
    protected static UIButton Button(string identifier, string title, Action tapped)
    {
        var button = UIButton.FromType(UIButtonType.System);
        button.SetTitle(title, UIControlState.Normal);
        button.AccessibilityIdentifier = identifier;
        button.HorizontalAlignment = UIControlContentHorizontalAlignment.Leading;
        button.TouchUpInside += (_, _) => tapped();
        return button;
    }

    /// <summary>A label with an identifier.</summary>
    protected static UILabel Label(string identifier, string text)
        => new() { Text = text, AccessibilityIdentifier = identifier, Lines = 0 };

    /// <summary>Pins <paramref name="view"/> to the edges of <paramref name="host"/>.</summary>
    protected static void Pin(UIView view, UIView host)
    {
        view.TopAnchor.ConstraintEqualTo(host.TopAnchor).Active = true;
        view.BottomAnchor.ConstraintEqualTo(host.BottomAnchor).Active = true;
        view.LeadingAnchor.ConstraintEqualTo(host.LeadingAnchor).Active = true;
        view.TrailingAnchor.ConstraintEqualTo(host.TrailingAnchor).Active = true;
    }
}
