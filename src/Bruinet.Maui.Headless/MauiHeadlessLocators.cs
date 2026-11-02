using Bruinet.Core.Locators;
using Microsoft.Maui.Controls;
using MauiHeadless.Inspection;

namespace Bruinet.Maui.Headless;

/// <summary>Bruinet's locators on MauiHeadless's tree: which nodes under a root a locator names.</summary>
internal static class MauiHeadlessLocators
{
    // The head reports Windows, so a page object asks for the back button by WinUI's id for it, as it
    // asks for Shell's tabs by WinUI's names (MauiHeadlessDriver.ShellChrome): MauiHeadless's own is BackButton.
    private const string WindowsBackButton = "NavigationViewBackButton";

    // And the title bar's text by WinUI's id for it; MauiHeadless's own is PageTitle.
    private const string WindowsTitle = "title";

    /// <summary>The nodes under <paramref name="root"/> (itself excluded) that <paramref name="locator"/> names, in reading order.</summary>
    public static IEnumerable<VisualNode> Match(VisualNode root, Locator locator)
    {
        IEnumerable<VisualNode> scopes = [root];
        if (locator.Parent is { } parent)
            scopes = Match(root, parent);

        if (locator is { Strategy: LocatorStrategy.XPath, Value: "./*" or "*" })
            return scopes.SelectMany(scope => scope.Children);

        var predicate = Predicate(locator);
        return scopes.SelectMany(scope => scope.DescendantsAndSelf().Skip(1)).Where(predicate).Distinct();
    }

    private static Func<VisualNode, bool> Predicate(Locator locator)
    {
        var value = locator.Value;
        return locator.Strategy switch
        {
            LocatorStrategy.AutomationId or LocatorStrategy.Id or LocatorStrategy.AccessibilityId
                or LocatorStrategy.DataAutomationId or LocatorStrategy.DataTestId
                => node => node.AutomationId == value
                           || (value == WindowsBackButton && node.Element is NavigationBackButton)
                           || (value == WindowsTitle && node.Element is NavigationTitle),
            LocatorStrategy.Name => node => MauiHeadlessElement.NameOf(node.Element) == value,
            LocatorStrategy.Text or LocatorStrategy.LinkText => node => node.Text == value,
            LocatorStrategy.PartialLinkText => node => node.Text?.Contains(value, StringComparison.Ordinal) == true,
            LocatorStrategy.ControlType => OfControlType(value),
            // A control type and a name, as UI Automation's AndCondition of the two (Windows' "Button" "Yes").
            LocatorStrategy.ControlTypeAndName when locator.TypeName is { } typeName
                => node => OfControlType(typeName)(node) && MauiHeadlessElement.NameOf(node.Element) == value,
            LocatorStrategy.ClassName or LocatorStrategy.TagName
                => node => IsOfType(node.Element, value),
            _ => throw new DeviceOnlyException($"Locator {locator.Strategy}:{value}"),
        };
    }

    private static Func<VisualNode, bool> OfControlType(string controlType)
    {
        // A ListView cell's view and a CollectionView's realized row are what a device reports as a list item.
        if (string.Equals(controlType, "ListItem", StringComparison.OrdinalIgnoreCase))
            return node => node.Element is View { Parent: Cell } || VisualTree.IsItemRow(node.Element);
        if (ControlTypes.TryGetValue(controlType, out var types))
            return node => types.Any(type => type.IsInstanceOfType(node.Element));
        return node => IsOfType(node.Element, controlType);
    }

    // The control-type words of the platform trees (UI Automation's, Appium's), as the MAUI types a
    // device draws them for: a Label is text there, an Entry an edit, a row a list item.
    private static readonly Dictionary<string, Type[]> ControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = [typeof(Label), typeof(Span)],
        ["button"] = [typeof(Microsoft.Maui.Controls.Button), typeof(ImageButton)],
        ["image"] = [typeof(Image), typeof(ImageButton)],
        ["edit"] = [typeof(Entry), typeof(Editor), typeof(SearchBar)],
        ["entry"] = [typeof(Entry), typeof(Editor), typeof(SearchBar)],
        ["checkbox"] = [typeof(CheckBox)],
        ["radiobutton"] = [typeof(RadioButton)],
        ["combobox"] = [typeof(Picker)],
        ["slider"] = [typeof(Slider)],
        ["progressbar"] = [typeof(ProgressBar)],
        ["tabitem"] = [typeof(ShellSection)],
        ["list"] = [typeof(ItemsView)],
    };

    // "Button" names Button and every type derived from it, as a platform's class name names the control.
    private static bool IsOfType(Element element, string typeName)
    {
        for (var type = element.GetType(); type is not null && type != typeof(object); type = type.BaseType)
        {
            if (type.Name == typeName || type.FullName == typeName)
                return true;
        }

        return false;
    }
}
