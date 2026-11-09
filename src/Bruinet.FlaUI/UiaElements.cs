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

    /// <summary>
    /// Runs a UI Automation read or action, reporting a removed element as
    /// <see cref="StaleElementException"/> and a closed app as <see cref="AppUnavailableException"/>.
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
        return string.Join('.', element.Properties.RuntimeId.Value);
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
        return within.FindFirstDescendant(locator.ToCondition(app.ConditionFactory));
    }

    /// <summary>Every descendant of <paramref name="within"/> matching <paramref name="locator"/> now. One attempt.</summary>
    /// <param name="app">The app, for its conditions.</param>
    /// <param name="within">Where to look.</param>
    /// <param name="locator">What to look for.</param>
    public static IReadOnlyList<AutomationElement> FindAll(DesktopApp app, AutomationElement within, Locator locator)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(within);
        ArgumentNullException.ThrowIfNull(locator);
        return within.FindAllDescendants(locator.ToCondition(app.ConditionFactory));
    }
}
