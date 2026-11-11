namespace Bruinet.Maui.CommunityToolkit.Controls.Views;

/// <summary>
/// CommunityToolkit.Maui <c>Expander</c>: a header that shows or hides its content when tapped.
/// Windows needs the app's verb sink (<c>Tap</c>, <c>GetState</c>); Android has no state read.
/// Routes and platform limits: <c>docs/controls/community-toolkit.md</c>.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Expander<TScope> : Bruinet.Maui.Controls.Base.ClickableControlBase<TScope>, Bruinet.Core.Interfaces.IExpandableControlObject<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates an expander control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the expander element.</param>
    public Expander(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates an expander control within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID).</param>
    public Expander(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Reads whether the expander shows its content, as the app reports it.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The expanded state, or null where the app does not report it.</returns>
    protected virtual bool? IsExpandedCore(IMauiElement? element)
    {
        if (element == null)
        {
            return null;
        }

        if (element.ReadState("IsExpanded") is not { } reported)
        {
            return IsExpandedByWhatIsShown(element);
        }

        return bool.TryParse(reported, out var expanded)
            ? expanded
            : throw new BruinetException(
                $"The expander answered IsExpanded with '{reported}', which is not a boolean. "
                + $"Locator: {Locator}");
    }

    /// <summary>
    /// Whether the expander is open, read from what is on screen: taller than its header.
    /// </summary>
    /// <remarks>
    /// For a platform with no app-side state read. On Android the expander's node grows from its
    /// header's height to header plus content when it opens (measured: 49 to 98 px, the header
    /// [53,557][1028,606] throughout).
    /// </remarks>
    private bool? IsExpandedByWhatIsShown(IMauiElement element)
        => OnATouchPlatform && FindHeader(element) is { } header
            ? element.Rect.Height > header.Rect.Height + 1
            : null;

    /// <summary>
    /// The expander's header: the first descendant shorter than the expander, or the first leaf.
    /// </summary>
    /// <remarks>
    /// The expander holds one layout node that grows with the content too, so wrappers as tall as
    /// the expander are stepped through. UiAutomator2 evaluates an element's XPath against a
    /// document rooted at that element, so "./*" is the element itself and its children are
    /// "./*/*" (measured: "./*" walked TestExpander > TestExpander > ...); XCUITest's "./*" is the
    /// children. Null where the expander has no children to read.
    /// </remarks>
    private IMauiElement? FindHeader(IMauiElement element)
    {
        var height = element.Rect.Height;
        var children = Context.Driver?.ChildrenPath ?? Locator.ByXPath("./*");
        IMauiElement? header = null;
        var node = element;

        for (var depth = 0; depth < MaxWrapperDepth; depth++)
        {
            var first = node.FindElements(children).FirstOrDefault();
            if (first == null)
            {
                break;
            }

            header = node = first;
            if (header.Rect.Height < height - 1)
            {
                break;
            }
        }

        return header;
    }

    private bool OnATouchPlatform => Context.Driver?.OmitsOffscreenElements == true;

    /// <summary>How deep the header may sit below the expander's own node.</summary>
    private const int MaxWrapperDepth = 4;

    /// <summary>
    /// Taps the header once, flipping the expanded state.
    /// </summary>
    /// <remarks>
    /// On a touch platform the tap goes to the header itself: an open expander's centre is the line
    /// between header and content, and a tap there missed the header and left it open.
    /// </remarks>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ToggleCore(IMauiElement element, int? timeoutMs = null)
    {
        if (OnATouchPlatform && FindHeader(element) is { } header)
        {
            header.Click();
            return;
        }

        element.PerformGesture(MauiGesture.Tap);
    }

    /// <summary>
    /// Shows the content. No-op when already expanded.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ExpandCore(IMauiElement element, int? timeoutMs = null)
        => SetExpandedCore(element, true, timeoutMs);

    /// <summary>
    /// Hides the content. No-op when already collapsed.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void CollapseCore(IMauiElement element, int? timeoutMs = null)
        => SetExpandedCore(element, false, timeoutMs);

    /// <summary>
    /// Taps the header when the state differs, then waits for the app to report the new state.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="expanded">The desired state. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <exception cref="NotSupportedException">The app does not report the expanded state.</exception>
    protected virtual void SetExpandedCore(IMauiElement element, bool? expanded, int? timeoutMs = null)
    {
        if (expanded == null)
        {
            return;
        }

        var current = IsExpandedCore(element)
            ?? throw new NotSupportedException(
                $"Expander '{Locator.Value}' does not report IsExpanded, so tapping it could not be "
                + "made idempotent. Declare GetState with a sink that answers it in the app under test.");

        if (current == expanded)
        {
            return;
        }

        ToggleCore(element, timeoutMs);

        var confirmation = Confirm(() => IsExpandedCore(element), actual => actual == expanded, timeoutMs);
        if (!confirmation.IsConfirmed)
        {
            throw confirmation.Failure(Locator, "the tap", lastError => new TimeoutException(
                $"Expander '{Locator.Value}' did not become {(expanded.Value ? "expanded" : "collapsed")}.",
                lastError));
        }
    }

    #endregion

    /// <summary>Taps the header once, turning the expander over (<see cref="Bruinet.Core.Interfaces.IExpandableControlObject{TScope}"/>).</summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    public TScope ToggleExpanded(int? timeoutMs = null) => Toggle(timeoutMs);
}
