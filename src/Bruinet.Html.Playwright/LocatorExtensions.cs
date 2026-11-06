using Bruinet.Core.Locators;
using Microsoft.Playwright;

namespace Bruinet.Html.Playwright;

internal static class LocatorExtensions
{
    public static ILocator ToPlaywrightLocator(PlaywrightTestContext context, Locator locator)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ToPlaywrightLocator(context.InternalPage, context.InternalFrame, locator);
    }

    public static ILocator ToPlaywrightLocator(IPage page, IFrame? frame, Locator locator)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(locator);

        // One root, not "*": a chain runs its next selector from every match of the last, and "*" took 800 ms a call on
        // a page of a few thousand elements, past a read's cap on a slow machine.
        var root = Scope(page, frame, ":root");

        if (locator.Parent != null)
        {
            root = ToPlaywrightLocator(root, locator.Parent);
        }

        return ToPlaywrightLocator(root, locator);
    }

    public static ILocator ToPlaywrightLocator(ILocator parent, Locator locator)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(locator);

        var baseLocator = locator.Parent != null
            ? ToPlaywrightLocator(parent, locator.Parent)
            : parent;

        return locator.Strategy switch
        {
            LocatorStrategy.Css => baseLocator.Locator(locator.Value),
            LocatorStrategy.Id => baseLocator.Locator($"#{EscapeCss(locator.Value)}"),
            LocatorStrategy.DataTestId => baseLocator.Locator($"[data-testid='{EscapeForAttribute(locator.Value)}']"),
            LocatorStrategy.DataAutomationId => baseLocator.Locator($"[data-automation-id='{EscapeForAttribute(locator.Value)}']"),
            LocatorStrategy.AutomationId => baseLocator.Locator($"[data-automation-id='{EscapeForAttribute(locator.Value)}'], [id='{EscapeForAttribute(locator.Value)}']"),
            LocatorStrategy.Name => baseLocator.Locator($"[name='{EscapeForAttribute(locator.Value)}']"),
            LocatorStrategy.ClassName => baseLocator.Locator($".{EscapeCss(locator.Value)}"),
            LocatorStrategy.TagName => baseLocator.Locator(locator.Value),
            LocatorStrategy.XPath => baseLocator.Locator($"xpath={locator.Value}"),
            LocatorStrategy.Text => baseLocator.GetByText(locator.Value),
            LocatorStrategy.LinkText => baseLocator.Locator($"a:has-text('{EscapeForSelectorText(locator.Value)}')"),
            LocatorStrategy.PartialLinkText => baseLocator.Locator($"a:has-text('{EscapeForSelectorText(locator.Value)}')"),
            // The accessible name: aria-label, aria-labelledby or a <label>, as a screen reader names it.
            LocatorStrategy.AccessibilityId => baseLocator.GetByLabel(locator.Value, new LocatorGetByLabelOptions { Exact = true }),
            LocatorStrategy.ControlType => baseLocator.Locator(locator.Value),
            _ => baseLocator.Locator(locator.Value)
        };
    }

    private static ILocator Scope(IPage page, IFrame? frame, string selector)
    {
        return frame != null ? frame.Locator(selector) : page.Locator(selector);
    }

    // A CSS string in single quotes: a backslash, then a quote, each escaped.
    internal static string EscapeForAttribute(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

    internal static string EscapeForSelectorText(string value) => EscapeForAttribute(value);

    private static string EscapeCss(string value)
    {
        return value.Replace("\\", "\\\\").Replace(".", "\\.").Replace("#", "\\#").Replace(":", "\\:");
    }
}
