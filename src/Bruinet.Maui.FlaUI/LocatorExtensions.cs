using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;

namespace Bruinet.Maui.FlaUI;

/// <summary>
/// Extension methods for converting Bruinet Locator to FlaUI PropertyConditions.
/// </summary>
public static class LocatorExtensions
{
    /// <summary>
    /// Converts a Bruinet Locator to a FlaUI PropertyCondition.
    /// </summary>
    /// <param name="locator">The locator to convert.</param>
    /// <param name="conditionFactory">The FlaUI condition factory.</param>
    /// <returns>A condition that can be used with FlaUI.</returns>
    /// <exception cref="ArgumentNullException">Thrown when locator is null.</exception>
    /// <exception cref="LocatorNotSupportedException">Thrown when locator strategy is not supported by FlaUI.</exception>
    public static ConditionBase ToCondition(this Locator locator, ConditionFactory conditionFactory)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(conditionFactory);

        return locator.Strategy switch
        {
            // AutomationId is the primary locator for MAUI/WPF.
            // Some MAUI/WinUI elements expose test identifiers via Name instead of AutomationId,
            // so include Name as a fallback for better cross-platform compatibility.
            LocatorStrategy.AutomationId => conditionFactory.ByAutomationId(locator.Value).Or(conditionFactory.ByName(locator.Value)),

            // AccessibilityId maps to AutomationId in Windows UI Automation,
            // with Name as a compatibility fallback.
            LocatorStrategy.AccessibilityId => conditionFactory.ByAutomationId(locator.Value).Or(conditionFactory.ByName(locator.Value)),

            // Name property
            LocatorStrategy.Name => conditionFactory.ByName(locator.Value),

            // ClassName
            LocatorStrategy.ClassName => conditionFactory.ByClassName(locator.Value),

            // ControlType - parse the string to ControlType enum
            LocatorStrategy.ControlType => conditionFactory.ByControlType(ParseControlType(locator.Value)),

            // Text - search by Name (FlaUI uses Name for visible text)
            LocatorStrategy.Text => conditionFactory.ByName(locator.Value),

            // ID - map to AutomationId for WPF/MAUI, with Name fallback.
            LocatorStrategy.Id => conditionFactory.ByAutomationId(locator.Value).Or(conditionFactory.ByName(locator.Value)),

            // ControlTypeAndName
            LocatorStrategy.ControlTypeAndName => new AndCondition(
                conditionFactory.ByControlType(ParseControlType(locator.TypeName!)),
                conditionFactory.ByName(locator.Value)
                ),

            // XPath, CSS, LinkText etc. are not supported by Windows UI Automation
            LocatorStrategy.XPath or
            LocatorStrategy.Css or
            LocatorStrategy.LinkText or
            LocatorStrategy.PartialLinkText or
            LocatorStrategy.TagName or
            LocatorStrategy.DataTestId or
            LocatorStrategy.DataAutomationId =>
                throw new LocatorNotSupportedException(locator.Strategy, "FlaUI"),

            _ => throw new LocatorNotSupportedException(locator.Strategy, "FlaUI")
        };
    }

    private static ControlType ParseControlType(string value)
    {
        if (Enum.TryParse<ControlType>(value, ignoreCase: true, out var controlType))
        {
            return controlType;
        }

        // Try common mappings
        return value.ToLowerInvariant() switch
        {
            "button" => ControlType.Button,
            "text" or "textbox" or "entry" => ControlType.Edit,
            "label" or "textblock" => ControlType.Text,
            "checkbox" or "check" => ControlType.CheckBox,
            "radio" or "radiobutton" => ControlType.RadioButton,
            "combobox" or "combo" or "picker" => ControlType.ComboBox,
            "list" or "listview" or "collectionview" => ControlType.List,
            "listitem" => ControlType.ListItem,
            "tree" or "treeview" => ControlType.Tree,
            "treeitem" => ControlType.TreeItem,
            "menu" => ControlType.Menu,
            "menuitem" => ControlType.MenuItem,
            "tab" or "tabcontrol" => ControlType.Tab,
            "tabitem" => ControlType.TabItem,
            "slider" => ControlType.Slider,
            "progressbar" or "progress" => ControlType.ProgressBar,
            "scrollbar" => ControlType.ScrollBar,
            "image" => ControlType.Image,
            "window" => ControlType.Window,
            "pane" or "frame" or "border" => ControlType.Pane,
            "group" => ControlType.Group,
            "document" => ControlType.Document,
            "hyperlink" or "link" => ControlType.Hyperlink,
            "tooltip" => ControlType.ToolTip,
            "toolbar" => ControlType.ToolBar,
            "statusbar" => ControlType.StatusBar,
            "table" or "grid" or "datagrid" => ControlType.Table,
            "header" => ControlType.Header,
            "headeritem" => ControlType.HeaderItem,
            "spinner" => ControlType.Spinner,
            "calendar" => ControlType.Calendar,
            "datepicker" => ControlType.Custom, // No direct mapping
            "timepicker" => ControlType.Custom, // No direct mapping
            _ => ControlType.Custom
        };
    }
}
