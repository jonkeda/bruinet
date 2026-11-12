using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;

namespace Bruinet.Maui.DevFlow;

/// <summary>Bruinet's locators on the agent's tree: which nodes under a root a locator names.</summary>
internal static class DevFlowLocators
{
    // On Windows a page object asks for the back button and the title bar by WinUI's ids, as FlaUI
    // shows them; the agent's own are BackButton and NavBarTitle.
    private const string WindowsBackButton = "NavigationViewBackButton";
    private const string WindowsTitle = "title";

    /// <summary>The nodes under <paramref name="root"/> (itself excluded) that <paramref name="locator"/> names, in reading order.</summary>
    /// <param name="root">Where to look.</param>
    /// <param name="locator">What to look for.</param>
    /// <param name="css">The ids the agent's CSS engine names, for a CSS locator.</param>
    public static IEnumerable<DevFlowNode> Match(DevFlowNode root, Locator locator, Func<string, IReadOnlyList<string>>? css = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(locator);

        IEnumerable<DevFlowNode> scopes = [root];
        if (locator.Parent is { } parent)
            scopes = Match(root, parent, css).ToList();

        if (locator is { Strategy: LocatorStrategy.XPath, Value: "./*" or "*" })
            return scopes.SelectMany(scope => scope.Children);

        var predicate = Predicate(locator, css);
        return scopes.SelectMany(scope => scope.DescendantsAndSelf().Skip(1)).Where(predicate).Distinct();
    }

    private static Func<DevFlowNode, bool> Predicate(Locator locator, Func<string, IReadOnlyList<string>>? css)
    {
        var value = locator.Value;
        switch (locator.Strategy)
        {
            case LocatorStrategy.AutomationId or LocatorStrategy.Id or LocatorStrategy.AccessibilityId
                or LocatorStrategy.DataAutomationId or LocatorStrategy.DataTestId:
                return node => node.AutomationId == value
                               || (value == WindowsBackButton && node.Type == "BackButton")
                               || (value == WindowsTitle && node.Type == "NavBarTitle");
            case LocatorStrategy.Name:
                return node => NameOf(node) == value;
            case LocatorStrategy.Text or LocatorStrategy.LinkText:
                return node => node.Text == value;
            case LocatorStrategy.PartialLinkText:
                return node => node.Text?.Contains(value, StringComparison.Ordinal) == true;
            case LocatorStrategy.ControlType:
                return OfControlType(value);
            case LocatorStrategy.ControlTypeAndName when locator.TypeName is { } typeName:
                // As UI Automation's AndCondition of the two (Windows' "Button" "Yes").
                return node => OfControlType(typeName)(node) && NameOf(node) == value;
            case LocatorStrategy.ClassName or LocatorStrategy.TagName:
                return node => IsOfType(node, value);
            case LocatorStrategy.Css when css is not null:
                var ids = css(value).ToHashSet(StringComparer.Ordinal);
                return node => ids.Contains(node.Id);
            default:
                throw new RouteUnavailableException(
                    $"The DevFlow runner has no route for a {locator.Strategy} locator ('{value}'): "
                    + "name the element by AutomationId, text, type or a CSS selector.");
        }
    }

    /// <summary>What a platform's tree names an element by: its text where it shows one, else its AutomationId.</summary>
    public static string? NameOf(DevFlowNode node) => string.IsNullOrEmpty(node.Text) ? node.AutomationId : node.Text;

    private static Func<DevFlowNode, bool> OfControlType(string controlType)
        => ControlTypes.TryGetValue(controlType, out var types)
            ? node => types.Any(type => IsOfType(node, type))
            : node => IsOfType(node, controlType);

    // The control-type words of the platform trees (UI Automation's, Appium's), as the MAUI types a
    // device draws them for: a Label is text there, an Entry an edit.
    private static readonly Dictionary<string, string[]> ControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = ["Label", "Span", "NavBarTitle", "Text"],
        ["button"] = ["Button", "ImageButton", "BackButton", "ToolbarItem", "DialogAction"],
        ["image"] = ["Image", "ImageButton"],
        ["edit"] = ["Entry", "Editor", "SearchBar", "Edit"],
        ["entry"] = ["Entry", "Editor", "SearchBar", "Edit"],
        ["checkbox"] = ["CheckBox"],
        ["radiobutton"] = ["RadioButton"],
        ["combobox"] = ["Picker"],
        ["slider"] = ["Slider"],
        ["progressbar"] = ["ProgressBar"],
        ["tabitem"] = ["Tab"],
        ["list"] = ["CollectionView", "ListView", "CarouselView"],
        ["listitem"] = ["FlyoutItem"],
    };

    // The agent gives the element's own type, not its base types: "Button" names Button, and an
    // app's subclass only by its own name or full name.
    private static bool IsOfType(DevFlowNode node, string typeName)
        => string.Equals(node.Type, typeName, StringComparison.Ordinal)
           || string.Equals(node.Info.FullType, typeName, StringComparison.Ordinal);
}
