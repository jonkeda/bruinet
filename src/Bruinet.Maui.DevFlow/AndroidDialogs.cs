using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Maui.DevFlow.Driver;

namespace Bruinet.Maui.DevFlow;

/// <summary>
/// An Android alert, which the agent's tree does not show, read from a <c>uiautomator dump</c> of the
/// screen (as DevFlow's own Android driver reads it) into the dialog shape the Windows tree gives: a
/// window holding the title, the message, a prompt's field and the buttons, accepting one first.
/// Its nodes' ids start with <see cref="IdPrefix"/>; acting on them is a tap or typing over adb
/// (<see cref="AndroidLaunch"/>), touch input as the Appium runner's is (AD-037).
/// </summary>
internal static partial class AndroidDialogs
{
    /// <summary>The ids of the nodes read from the dump: native, and a dialog, as the tree's rules expect.</summary>
    public const string IdPrefix = "native:android:dialog:";

    public static bool IsDialogNode(string? id) => id?.StartsWith(IdPrefix, StringComparison.Ordinal) == true;

    /// <summary>The alert on screen in the dump, as a window of the agent's shape; null when none is open.</summary>
    public static ElementInfo? Read(XDocument screen)
    {
        var nodes = screen.Descendants("node").ToList();
        var panel = nodes.FirstOrDefault(node => ResourceId(node).EndsWith(":id/parentPanel", StringComparison.Ordinal));
        if (panel is null)
            return null;

        var inside = panel.Descendants("node").ToList();
        var title = inside.FirstOrDefault(node => ResourceId(node).EndsWith(":id/alertTitle", StringComparison.Ordinal));
        var message = inside.FirstOrDefault(node => ResourceId(node).EndsWith(":id/message", StringComparison.Ordinal));
        var field = inside.FirstOrDefault(node => (string?)node.Attribute("class") == "android.widget.EditText");

        // Accepting first, the dismissing one last, in the order MAUI was given them.
        var buttons = new[] { "button1", "button3", "button2" }
            .Select(id => inside.FirstOrDefault(node => ResourceId(node).EndsWith(":id/" + id, StringComparison.Ordinal)))
            .OfType<XElement>()
            .ToList();

        var window = Info("window", "Window", (string?)title?.Attribute("text"), panel);
        window.Children = [];
        if (title is not null)
            window.Children.Add(Info("title", "Text", (string?)title.Attribute("text"), title));
        if (message is not null)
            window.Children.Add(Info("message", "Text", (string?)message.Attribute("text"), message));
        if (field is not null)
            window.Children.Add(Info("field", "Edit", (string?)field.Attribute("text"), field));
        foreach (var button in buttons)
            window.Children.Add(Info(ResourceId(button)[(ResourceId(button).LastIndexOf('/') + 1)..], "Button", (string?)button.Attribute("text"), button));
        return window;
    }

    /// <summary>The centre of a node read from the dump, in the screen's pixels, where a tap goes.</summary>
    public static (int X, int Y) Centre(ElementInfo node)
        => node.WindowBounds is { } bounds
            ? ((int)(bounds.X + bounds.Width / 2), (int)(bounds.Y + bounds.Height / 2))
            : throw new InvalidOperationException($"{node.Id} has no bounds to tap.");

    private static ElementInfo Info(string name, string type, string? text, XElement node)
    {
        var (x1, y1, x2, y2) = Bounds(node);
        return new ElementInfo
        {
            Id = IdPrefix + name,
            Type = type,
            FullType = "android:" + (string?)node.Attribute("class"),
            Text = string.IsNullOrEmpty(text) ? null : text,
            IsVisible = true,
            IsEnabled = (string?)node.Attribute("enabled") != "false",
            IsFocused = (string?)node.Attribute("focused") == "true",
            WindowBounds = new BoundsInfo { X = x1, Y = y1, Width = x2 - x1, Height = y2 - y1 },
        };
    }

    private static string ResourceId(XElement node) => (string?)node.Attribute("resource-id") ?? string.Empty;

    // bounds="[x1,y1][x2,y2]", in pixels.
    private static (int, int, int, int) Bounds(XElement node)
    {
        var match = BoundsPattern().Match((string?)node.Attribute("bounds") ?? string.Empty);
        if (!match.Success)
            return (0, 0, 0, 0);
        int At(int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
        return (At(1), At(2), At(3), At(4));
    }

    [GeneratedRegex(@"\[(\d+),(\d+)\]\[(\d+),(\d+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex BoundsPattern();
}
