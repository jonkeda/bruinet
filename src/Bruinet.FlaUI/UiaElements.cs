using FlaUI.Core.Definitions;

namespace Bruinet.FlaUI;

/// <summary>
/// What every FlaUI stack's element does the same way: report a removed element as stale and a
/// closed app as unavailable (R5), name an element by its runtime id, tell whether it still hangs
/// under the app's window, and look up descendants in one attempt, never waiting (R2).
/// </summary>
public static class UiaElements
{
    // Deep UI trees are tens of levels, not hundreds; the bound only stops a cycle.
    private const int MaxAttachedDepth = 256;
    private const string WebViewClassName = "Microsoft.UI.Xaml.Controls.WebView2";

    /// <summary>
    /// Runs a UI Automation read or action, reporting a removed element as
    /// <see cref="StaleElementException"/> and a closed app as <see cref="AppUnavailableException"/>,
    /// whatever error UI Automation gave for it. Any other error is the call's to see.
    /// </summary>
    /// <typeparam name="T">What the read answers.</typeparam>
    /// <param name="app">The app the element belongs to, asked whether it has exited.</param>
    /// <param name="touch">The read or action.</param>
    public static T Live<T>(DesktopApp app, Func<T> touch)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(touch);
        try
        {
            return touch();
        }
        catch (Exception error) when (FlaUIErrors.IsElementGone(error))
        {
            if (app.HasExited)
            {
                throw new AppUnavailableException("the application process has exited.", error);
            }

            throw new StaleElementException(platformError: error);
        }
        catch (Exception error) when (error is not (AppUnavailableException or StaleElementException) && app.HasExited)
        {
            // Whatever UI Automation says while the app is going (an RPC error, a timeout), the cause is the app.
            throw new AppUnavailableException("the application process has exited.", error);
        }
    }

    /// <inheritdoc cref="Live{T}(DesktopApp, Func{T})"/>
    public static void Live(DesktopApp app, Action touch)
    {
        ArgumentNullException.ThrowIfNull(touch);
        Live(app, () =>
        {
            touch();
            return true;
        });
    }

    /// <summary>
    /// The element's UI Automation runtime id, as text: the same for the same element, new when
    /// the framework replaces it (a WinUI toolbar item replaced when its command's CanExecute
    /// changes goes <c>…4.30</c> to <c>…4.65</c>). Read live, so it doubles as the "is this element
    /// still there" check; wrap it in <see cref="Live{T}(DesktopApp, Func{T})"/>.
    /// </summary>
    /// <param name="element">The element.</param>
    public static string InstanceKey(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        try
        {
            return string.Join('.', element.Properties.RuntimeId.Value);
        }
        catch (global::FlaUI.Core.Exceptions.PropertyNotSupportedException error)
        {
            // Every live element has a runtime id; a removed one reports the property as not supported.
            throw new global::FlaUI.Core.Exceptions.ElementNotAvailableException(error);
        }
    }

    /// <summary>
    /// Whether <paramref name="element"/> still hangs under the app's window, walking the control
    /// view up. A page navigated away from leaves the tree before its element stops answering: for
    /// about a second it reads as present, on screen and full size, and only its missing way up to
    /// the window gives it away.
    /// </summary>
    /// <param name="app">The app whose window the walk ends at.</param>
    /// <param name="element">The element.</param>
    public static bool IsAttached(DesktopApp app, AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(element);
        var walker = app.Automation.TreeWalkerFactory.GetControlViewWalker();
        var window = app.RootElement;
        var node = element;

        for (var depth = 0; depth < MaxAttachedDepth; depth++)
        {
            if (node.Equals(window))
            {
                return true;
            }

            if (walker.GetParent(node) is not { } parent)
            {
                return false;
            }

            node = parent;
        }

        return true;
    }

    /// <summary>The first descendant of <paramref name="within"/> matching <paramref name="locator"/> now, or null. One attempt.</summary>
    /// <param name="app">The app, for its conditions.</param>
    /// <param name="within">Where to look.</param>
    /// <param name="locator">What to look for.</param>
    public static AutomationElement? FindFirst(DesktopApp app, AutomationElement within, Locator locator)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(within);
        ArgumentNullException.ThrowIfNull(locator);
        var found = FindFirstPastWebViews(app, within, TreeScope.Descendants, locator.ToCondition(app.ConditionFactory));
        if (found is not null && locator.IsIdentity() && !HasAutomationId(found, locator.Value)
            && FindFirstPastWebViews(app, within, TreeScope.Descendants, app.ConditionFactory.ByAutomationId(locator.Value)) is { } byId)
        {
            // The Name fallback found a title or caption first; the element that carries the id is the one asked for.
            return byId;
        }

        return found;
    }

    private static bool HasAutomationId(AutomationElement element, string value)
        => string.Equals(element.Properties.AutomationId.ValueOrDefault, value, StringComparison.Ordinal);

    /// <summary>Every descendant of <paramref name="within"/> matching <paramref name="locator"/> now. One attempt.</summary>
    /// <param name="app">The app, for its conditions.</param>
    /// <param name="within">Where to look.</param>
    /// <param name="locator">What to look for.</param>
    public static IReadOnlyList<AutomationElement> FindAll(DesktopApp app, AutomationElement within, Locator locator)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(within);
        ArgumentNullException.ThrowIfNull(locator);
        var found = new List<AutomationElement>();
        FindAllPastWebViews(app, within, TreeScope.Descendants, locator.ToCondition(app.ConditionFactory), found);
        if (locator.IsIdentity() && found.Any(element => HasAutomationId(element, locator.Value)))
        {
            // Elements found only by the Name fallback (a title or caption) stay out when the id itself matched.
            return found.Where(element => HasAutomationId(element, locator.Value)).ToList();
        }

        return found;
    }

    // A WinUI WebView2 hosts its page in another process, and a UI Automation search from above it
    // stops there: nothing after it in the tree is found, although a tree walker still reaches it.
    // So a search that misses resumes in each subtree that follows a web view.
    private static AutomationElement? FindFirstPastWebViews(
        DesktopApp app, AutomationElement within, TreeScope scope, ConditionBase condition)
    {
        if (within.FindFirst(scope, condition) is { } found)
        {
            return found;
        }

        foreach (var after in SubtreesAfterWebView(app, within))
        {
            if (FindFirstPastWebViews(app, after, TreeScope.Subtree, condition) is { } later)
            {
                return later;
            }
        }

        return null;
    }

    private static void FindAllPastWebViews(
        DesktopApp app, AutomationElement within, TreeScope scope, ConditionBase condition, List<AutomationElement> found)
    {
        foreach (var element in within.FindAll(scope, condition))
        {
            if (!found.Contains(element))
            {
                found.Add(element);
            }
        }

        foreach (var after in SubtreesAfterWebView(app, within))
        {
            FindAllPastWebViews(app, after, TreeScope.Subtree, condition, found);
        }
    }

    // The siblings after the first web view under within, then after each of its ancestors up to within.
    private static IEnumerable<AutomationElement> SubtreesAfterWebView(DesktopApp app, AutomationElement within)
    {
        if (within.FindFirstDescendant(app.ConditionFactory.ByClassName(WebViewClassName)) is not { } webView)
        {
            yield break;
        }

        var walker = app.Automation.TreeWalkerFactory.GetRawViewWalker();
        var node = webView;
        for (var depth = 0; depth < MaxAttachedDepth && node is not null && !node.Equals(within); depth++)
        {
            for (var sibling = walker.GetNextSibling(node); sibling is not null; sibling = walker.GetNextSibling(sibling))
            {
                yield return sibling;
            }

            node = walker.GetParent(node);
        }
    }
}
