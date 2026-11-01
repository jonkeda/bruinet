using Bruinet.Core.Abstractions.Controls;
using Bruinet.Core.Utilities;
using Bruinet.Wpf.FlaUI;

namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for all WPF controls providing state checking, waiting, assertions, and text access.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class ControlBase<TScope> : ControlObjectBase<TScope>, IControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    private readonly IWpfScope<TScope> _scope;

    /// <summary>
    /// Creates a new control with the specified scope and locator.
    /// </summary>
    protected ControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(locator, scope)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <summary>
    /// Creates a new control using the scope's default locator strategy.
    /// </summary>
    protected ControlBase(IWpfScope<TScope> scope, string locatorValue)
        : this(scope, new Locator(scope.DefaultLocatorStrategy, locatorValue))
    {
    }

    /// <summary>
    /// Returns the containing scope for fluent chaining.
    /// </summary>
    protected TScope ContainingScope => _scope.Self;

    /// <summary>
    /// Gets the WPF scope.
    /// </summary>
    protected IWpfScope<TScope> WpfScope => _scope;

    /// <summary>
    /// Gets the WPF test context.
    /// </summary>
    protected IWpfTestContext Context => _scope.Context;

    /// <summary>
    /// Gets the default timeout in milliseconds.
    /// </summary>
    protected int DefaultTimeoutMs => Context.Timeouts.DefaultWait;

    /// <summary>
    /// Gets the polling interval in milliseconds.
    /// </summary>
    protected int PollingIntervalMs => Context.Timeouts.PollingInterval;

    /// <summary>
    /// The string identifier for this control (for logging).
    /// </summary>
    protected string AutomationId => Locator.Value;

    /// <summary>
    /// The page name (for logging).
    /// </summary>
    protected string PageName => Page?.Name ?? "";

    #region Element Finding

    /// <summary>
    /// Try to find the element without timeout.
    /// </summary>
    protected IWpfElement? TryFindElement()
    {
        return _scope.TryFindElement(Locator);
    }

    /// <summary>
    /// Find the element with polling and timeout.
    /// </summary>
    protected IWpfElement FindElement(int? timeoutMs = null)
    {
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Context.Driver.FindElement(Locator, timeout);
    }

    /// <summary>
    /// Find the element, scroll into view, and execute an action.
    /// </summary>
    protected T RunWithElement<T>(Func<IWpfElement, T> action, int? timeoutMs = null)
    {
        var element = FindElement(timeoutMs);
        EnsureVisible(element);
        return action(element);
    }

    /// <summary>
    /// Find the element, scroll into view, and execute an action (void).
    /// </summary>
    protected void RunWithElement(Action<IWpfElement> action, int? timeoutMs = null)
    {
        var element = FindElement(timeoutMs);
        EnsureVisible(element);
        action(element);
    }

    /// <summary>
    /// Ensure the element is visible by scrolling it into view if needed.
    /// </summary>
    protected virtual void EnsureVisible(IWpfElement element)
    {
        try
        {
            if (!element.Visible)
            {
                element.ScrollIntoView();
            }
        }
        catch
        {
            // Ignore scroll failures - element may still be interactive
        }
    }

    /// <summary>
    /// Poll a condition using default polling interval.
    /// </summary>
    protected bool Poll(Func<bool> condition, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (condition())
                    return true;
            }
            catch
            {
                // Transient failures expected during polling
            }

            WaitHelper.Pause(PollingIntervalMs);
        }

        return condition();
    }

    /// <summary>
    /// Poll a condition that requires the element.
    /// </summary>
    protected bool PollWithElement(Func<IWpfElement, bool> condition, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var element = TryFindElement();
                if (element != null && condition(element))
                    return true;
            }
            catch
            {
                // Transient failures expected during polling
            }

            WaitHelper.Pause(PollingIntervalMs);
        }

        // Final check
        var finalElement = TryFindElement();
        return finalElement != null && condition(finalElement);
    }

    #endregion

    #region Logging Helpers

    /// <summary>
    /// Execute and log an action.
    /// </summary>
    protected TScope Run(string action, Action<IWpfElement> operation, string? value = null, int? timeoutMs = null)
    {
        Context.Logger.LogAction("", PageName, AutomationId, action, value);
        RunWithElement(operation, timeoutMs);
        return ContainingScope;
    }

    /// <summary>
    /// Execute and log an assertion.
    /// </summary>
    protected TScope RunAssert(string assertType, string? expected, Func<IWpfElement, bool> check,
        string? message = null, int? timeoutMs = null)
    {
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        var success = PollWithElement(check, timeout);

        if (!success)
        {
            var actual = TryFindElement()?.Text;
            Context.Logger.LogAssertFail("", PageName, AutomationId, assertType, actual, expected, message);
            throw new AssertionException(
                message ?? $"Assertion '{assertType}' failed for '{AutomationId}'. Expected: {expected}, Actual: {actual}",
                expected, actual, AutomationId);
        }

        Context.Logger.LogAssertPass("", PageName, AutomationId, assertType, null, expected);
        return ContainingScope;
    }

    #endregion

    #region IControlObject<TScope> - Exists

    /// <inheritdoc />
    public virtual bool IsExists()
    {
        return TryFindElement() != null;
    }

    /// <inheritdoc />
    public bool WaitExists(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsExists() == expected.Value, timeout);
    }

    /// <inheritdoc />
    public TScope AssertExists(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitExists(expected, timeoutMs))
        {
            var actual = IsExists();
            throw new AssertionException(
                message ?? $"Expected '{AutomationId}' exists={expected} but was {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion

    #region IControlObject<TScope> - Visible

    /// <inheritdoc />
    public virtual bool? IsVisible()
    {
        try
        {
            var element = TryFindElement();
            if (element == null)
                return null;
            return element.Visible;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool WaitVisible(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsVisible() == expected, timeout);
    }

    /// <inheritdoc />
    public TScope AssertVisible(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitVisible(expected, timeoutMs))
        {
            var actual = IsVisible() ?? false;
            throw new AssertionException(
                message ?? $"Expected '{AutomationId}' visible={expected} but was {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion

    #region IControlObject<TScope> - Enabled

    /// <inheritdoc />
    public virtual bool? IsEnabled()
    {
        try
        {
            var element = TryFindElement();
            if (element == null)
                return null;
            return element.Enabled;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool WaitEnabled(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsEnabled() == expected, timeout);
    }

    /// <inheritdoc />
    public TScope AssertEnabled(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitEnabled(expected, timeoutMs))
        {
            var actual = IsEnabled() ?? false;
            throw new AssertionException(
                message ?? $"Expected '{AutomationId}' enabled={expected} but was {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion

    #region IControlObject<TScope> - Text

    /// <summary>The control's text, or null when it cannot be read.</summary>
    public virtual string? GetText(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            return element.Text;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Waits until the text equals <paramref name="expected"/>; false when it did not in time.</summary>
    public bool WaitText(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetText() == expected, timeout);
    }

    /// <summary>Asserts the text equals <paramref name="expected"/> within the timeout.</summary>
    public TScope AssertText(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitText(expected, timeoutMs))
        {
            var actual = GetText();
            throw new AssertionException(
                message ?? $"Expected text '{expected}' for '{AutomationId}' but got '{actual}'",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    /// <summary>Asserts the text contains <paramref name="expected"/> within the timeout.</summary>
    public TScope AssertTextContains(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        if (!Poll(() => GetText()?.Contains(expected) == true, timeout))
        {
            var actual = GetText();
            throw new AssertionException(
                message ?? $"Expected text of '{AutomationId}' to contain '{expected}' but got '{actual}'",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion

    #region IControlObject<TScope> - Attribute

    /// <inheritdoc />
    public string? GetAttribute(string name, int? timeoutMs)
    {
        try
        {
            var element = TryFindElement();
            return element?.GetAttribute(name);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
