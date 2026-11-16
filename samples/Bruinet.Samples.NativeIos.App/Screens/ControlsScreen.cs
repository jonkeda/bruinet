namespace Bruinet.Samples.NativeIos.App.Screens;

/// <summary>One of each input control; the status line says what changed last.</summary>
public sealed class ControlsScreen() : Screen("Controls")
{
    private static readonly string[] Colors =
        ["Red", "Green", "Blue", "Cyan", "Magenta", "Yellow", "Black", "White", "Orange", "Purple", "Brown", "Grey", "Pink", "Teal", "Olive", "Navy"];

    private UILabel _status = null!;

    /// <inheritdoc />
    protected override void Build()
    {
        _status = Label("status", "Nothing changed");
        var scroll = new UIScrollView { AccessibilityIdentifier = "scroll", TranslatesAutoresizingMaskIntoConstraints = false };
        var guide = View!.SafeAreaLayoutGuide;
        _status.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(_status);
        View.AddSubview(scroll);
        _status.TopAnchor.ConstraintEqualTo(guide.TopAnchor, 8).Active = true;
        _status.LeadingAnchor.ConstraintEqualTo(guide.LeadingAnchor, 16).Active = true;
        _status.TrailingAnchor.ConstraintEqualTo(guide.TrailingAnchor, -16).Active = true;
        scroll.TopAnchor.ConstraintEqualTo(_status.BottomAnchor, 8).Active = true;
        scroll.LeadingAnchor.ConstraintEqualTo(guide.LeadingAnchor).Active = true;
        scroll.TrailingAnchor.ConstraintEqualTo(guide.TrailingAnchor).Active = true;
        scroll.BottomAnchor.ConstraintEqualTo(guide.BottomAnchor).Active = true;
        scroll.AddSubview(Stack);
        Stack.TopAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.TopAnchor, 8).Active = true;
        Stack.BottomAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.BottomAnchor, -16).Active = true;
        Stack.LeadingAnchor.ConstraintEqualTo(scroll.FrameLayoutGuide.LeadingAnchor, 16).Active = true;
        Stack.TrailingAnchor.ConstraintEqualTo(scroll.FrameLayoutGuide.TrailingAnchor, -16).Active = true;

        Stack.AddArrangedSubview(Switch("agree", "I agree", on => Say($"Agree {On(on)}")));
        Stack.AddArrangedSubview(Switch("notifications", "Notifications", on => Say($"Notifications {On(on)}")));

        var size = new UISegmentedControl("Small", "Medium", "Large") { AccessibilityIdentifier = "size", SelectedSegment = 0 };
        size.ValueChanged += (_, _) => Say($"Size {size.TitleAt(size.SelectedSegment)}");
        Stack.AddArrangedSubview(size);

        var color = new UIPickerView { AccessibilityIdentifier = "color", Model = new ColorModel(Colors, name => Say($"Color {name}")) };
        color.HeightAnchor.ConstraintEqualTo(160).Active = true;
        Stack.AddArrangedSubview(color);

        var volume = new UISlider { AccessibilityIdentifier = "volume", MinValue = 0, MaxValue = 100, Value = 20 };
        volume.ValueChanged += (_, _) => Say($"Volume {Math.Round(volume.Value)}");
        Stack.AddArrangedSubview(volume);

        var quantity = new UIStepper { AccessibilityIdentifier = "quantity", MinimumValue = 0, MaximumValue = 10, Value = 1 };
        quantity.ValueChanged += (_, _) => Say($"Quantity {quantity.Value}");

        // A stretched stepper still draws at its own size on the left, but reports the stretched
        // frame: its plus button's center would be empty space. A row with a spacer keeps its size.
        Stack.AddArrangedSubview(new UIStackView([quantity, new UIView()]) { Axis = UILayoutConstraintAxis.Horizontal });

        Stack.AddArrangedSubview(new UIProgressView(UIProgressViewStyle.Default) { AccessibilityIdentifier = "progress", Progress = 0.4f });

        var notes = new UITextField { Placeholder = "Notes", AccessibilityIdentifier = "notes", BorderStyle = UITextBorderStyle.RoundedRect, AutocorrectionType = UITextAutocorrectionType.No };
        notes.ShouldReturn = field =>
        {
            field.ResignFirstResponder();
            Say($"Notes {field.Text}");
            return true;
        };
        Stack.AddArrangedSubview(notes);

        var comments = new UITextView { AccessibilityIdentifier = "comments", Font = UIFont.PreferredBody, AutocorrectionType = UITextAutocorrectionType.No };
        comments.Layer.BorderColor = UIColor.SystemGray4.CGColor;
        comments.Layer.BorderWidth = 1;
        comments.HeightAnchor.ConstraintEqualTo(80).Active = true;
        Stack.AddArrangedSubview(comments);

        var logo = new UIImageView(UIImage.GetSystemImage("star.fill")) { AccessibilityIdentifier = "logo", IsAccessibilityElement = true, AccessibilityLabel = "Bruinet logo", ContentMode = UIViewContentMode.ScaleAspectFit };
        logo.HeightAnchor.ConstraintEqualTo(64).Active = true;
        Stack.AddArrangedSubview(logo);

        // Pushes the last button below the fold, for scroll-to-find.
        var spacer = new UIView();
        spacer.HeightAnchor.ConstraintEqualTo(1200).Active = true;
        Stack.AddArrangedSubview(spacer);
        Stack.AddArrangedSubview(Button("bottom_button", "At the bottom", () => Say("Bottom tapped")));
    }

    private static UIStackView Switch(string identifier, string text, Action<bool> changed)
    {
        var toggle = new UISwitch { AccessibilityIdentifier = identifier, AccessibilityLabel = text };
        toggle.ValueChanged += (_, _) => changed(toggle.On);
        var row = new UIStackView([new UILabel { Text = text }, toggle]) { Axis = UILayoutConstraintAxis.Horizontal, Distribution = UIStackViewDistribution.EqualSpacing };
        return row;
    }

    private void Say(string text) => _status.Text = text;

    private static string On(bool on) => on ? "on" : "off";

    private sealed class ColorModel(string[] colors, Action<string> chosen) : UIPickerViewModel
    {
        public override nint GetComponentCount(UIPickerView pickerView) => 1;

        public override nint GetRowsInComponent(UIPickerView pickerView, nint component) => colors.Length;

        public override string GetTitle(UIPickerView pickerView, nint row, nint component) => colors[row];

        public override void Selected(UIPickerView pickerView, nint row, nint component) => chosen(colors[row]);
    }
}
