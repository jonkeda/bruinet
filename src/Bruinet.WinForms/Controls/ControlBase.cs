using Bruinet.Core.Abstractions.Controls;
using Bruinet.Core.Utilities;
using Bruinet.WinForms.FlaUI;

namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for all WinForms controls providing state checking, waiting, assertions, and text access.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class ControlBase<TScope> : ControlObjectBase<TScope>, IControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    private readonly IWinFormsScope<TScope> _scope;

    /// <summary>
    /// Creates a new control with the specified scope and locator.
    /// </summary>
    protected ControlBase(IWinFormsScope<TScope> scope, Locator locator)
        : base(locator, scope)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <summary>
    /// Creates a new control using the scope's default locator strategy.
    /// </summary>
    protected ControlBase(IWinFormsScope<TScope> scope, string locatorValue)
        : this(scope, new Locator(scope.DefaultLocatorStrategy, locatorValue))
    {
    }

    protected TScope ContainingScope => _scope.Self;
    protected IWinFormsScope<TScope> WinFormsScope => _scope;
    protected IWinFormsTestContext Context => _scope.Context;
    protected int DefaultTimeoutMs => Context.Timeouts.DefaultWait;
    protected int PollingIntervalMs => Context.Timeouts.PollingInterval;
    protected string AutomationId => Locator.Value;
    protected string PageName => Page?.Name ?? "";

    #region Element Finding

    protected IWinFormsElement? TryFindElement()
    {
        return _scope.TryFindElement(Locator);
    }

    protected IWinFormsElement FindElement(int? timeoutMs = null)
    {
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Context.Driver.FindElement(Locator, timeout);
    }

    protected T RunWithElement<T>(Func<IWinFormsElement, T> action, int? timeoutMs = null)
    {
        var element = FindElement(timeoutMs);
        EnsureVisible(element);
        return action(element);
    }

    protected void RunWithElement(Action<IWinFormsElement> action, int? timeoutMs = null)
    {
        var element = FindElement(timeoutMs);
        EnsureVisible(element);
        action(element);
    }

    protected virtual void EnsureVisible(IWinFormsElement element)
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

    protected bool PollWithElement(Func<IWinFormsElement, bool> condition, int timeoutMs)
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

        var finalElement = TryFindElement();
        return finalElement != null && condition(finalElement);
    }

    #endregion

    #region Logging Helpers

    protected TScope Run(string action, Action<IWinFormsElement> operation, string? value = null, int? timeoutMs = null)
    {
        Context.Logger.LogAction("", PageName, AutomationId, action, value);
        RunWithElement(operation, timeoutMs);
        return ContainingScope;
    }

    protected TScope RunAssert(string assertType, string? expected, Func<IWinFormsElement, bool> check,
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

    public virtual bool IsExists()
    {
        return TryFindElement() != null;
    }

    public bool WaitExists(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsExists() == expected.Value, timeout);
    }

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

    public bool WaitVisible(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsVisible() == expected, timeout);
    }

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

    public bool WaitEnabled(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsEnabled() == expected, timeout);
    }

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

    public bool WaitText(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetText() == expected, timeout);
    }

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
