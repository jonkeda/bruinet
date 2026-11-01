namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for toggle WPF controls (CheckBox, RadioButton).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class ToggleControlBase<TScope> : ClickableControlBase<TScope>, IToggleControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    /// <summary>
    /// Creates a new toggle control with the specified scope and locator.
    /// </summary>
    protected ToggleControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator) { }

    /// <summary>
    /// Creates a new toggle control using the scope's default locator strategy.
    /// </summary>
    protected ToggleControlBase(IWpfScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue) { }

    #region Checked State

    /// <inheritdoc />
    public virtual bool? IsChecked()
    {
        try
        {
            var element = TryFindElement();
            if (element == null)
                return null;
            return IsCheckedCore(element);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Core implementation for reading checked state from a UIA element.
    /// </summary>
    protected virtual bool IsCheckedCore(IWpfElement element)
    {
        return element.Selected;
    }

    /// <inheritdoc />
    public bool WaitChecked(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => IsChecked() == expected.Value, timeout);
    }

    /// <inheritdoc />
    public TScope AssertChecked(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitChecked(expected, timeoutMs))
        {
            var actual = IsChecked();
            throw new AssertionException(
                message ?? $"Expected '{AutomationId}' checked={expected} but was {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion

    #region Toggle Actions

    /// <inheritdoc />
    public virtual TScope Toggle(int? timeoutMs = null)
    {
        return Run("Toggle", e => ToggleCore(e), timeoutMs: timeoutMs);
    }

    /// <summary>
    /// Core toggle implementation.
    /// </summary>
    protected virtual void ToggleCore(IWpfElement element)
    {
        element.Click();
    }

    /// <inheritdoc />
    public virtual TScope SetChecked(bool? value, int? timeoutMs = null)
    {
        if (value == null)
            return ContainingScope;
        return value.Value ? Check(timeoutMs) : Uncheck(timeoutMs);
    }

    /// <inheritdoc />
    public virtual TScope Check(int? timeoutMs = null)
    {
        if (IsChecked() != true)
        {
            Toggle(timeoutMs);
        }
        return ContainingScope;
    }

    /// <inheritdoc />
    public virtual TScope Uncheck(int? timeoutMs = null)
    {
        if (IsChecked() == true)
        {
            Toggle(timeoutMs);
        }
        return ContainingScope;
    }

    #endregion
}
