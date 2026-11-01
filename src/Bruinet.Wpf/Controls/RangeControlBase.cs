namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for range WPF controls (Slider, ProgressBar).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class RangeControlBase<TScope> : ControlBase<TScope>, IRangeControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    /// <summary>
    /// Creates a new range control with the specified scope and locator.
    /// </summary>
    protected RangeControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator) { }

    /// <summary>
    /// Creates a new range control using the scope's default locator strategy.
    /// </summary>
    protected RangeControlBase(IWpfScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue) { }

    #region Value Access

    /// <inheritdoc />
    public virtual double? GetValue(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is IRangePatternElement range && range.SupportsRangeValue)
                return range.GetRangeValue();
            // Fallback: parse from text
            if (double.TryParse(element.Text, out var value))
                return value;
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public virtual TScope SetValue(double? value, int? timeoutMs = null)
    {
        if (value == null)
            return ContainingScope;
        RunWithElement(e =>
        {
            if (e is IRangePatternElement range && range.SupportsRangeValue)
                range.SetRangeValue(value.Value);
        }, timeoutMs);
        return ContainingScope;
    }

    /// <inheritdoc />
    public virtual double? GetMinimum(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is IRangePatternElement range && range.SupportsRangeValue)
                return range.GetRangeMinimum();
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public virtual double? GetMaximum(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is IRangePatternElement range && range.SupportsRangeValue)
                return range.GetRangeMaximum();
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public virtual double? GetStep(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is IRangePatternElement range && range.SupportsRangeValue)
                return range.GetRangeSmallChange();
            return null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Increment/Decrement

    /// <inheritdoc />
    public virtual TScope Increment(int? timeoutMs = null)
    {
        RunWithElement(e => IncrementCore(e), timeoutMs);
        return ContainingScope;
    }

    /// <summary>
    /// Core increment implementation.
    /// </summary>
    protected virtual void IncrementCore(IWpfElement element)
    {
        if (element is IRangePatternElement range && range.SupportsRangeValue)
        {
            var current = range.GetRangeValue() ?? 0;
            var step = range.GetRangeSmallChange() ?? 1;
            range.SetRangeValue(current + step);
        }
    }

    /// <inheritdoc />
    public virtual TScope Decrement(int? timeoutMs = null)
    {
        RunWithElement(e => DecrementCore(e), timeoutMs);
        return ContainingScope;
    }

    /// <summary>
    /// Core decrement implementation.
    /// </summary>
    protected virtual void DecrementCore(IWpfElement element)
    {
        if (element is IRangePatternElement range && range.SupportsRangeValue)
        {
            var current = range.GetRangeValue() ?? 0;
            var step = range.GetRangeSmallChange() ?? 1;
            range.SetRangeValue(current - step);
        }
    }

    #endregion

    #region Wait/Assert

    /// <inheritdoc />
    public bool WaitValue(double? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true; // nullable-skip: nothing asked, nothing failed
        return WaitValueWithin(expected, 0, timeoutMs);
    }

    /// <inheritdoc />
    public TScope AssertValue(double? expected, string? message = null, int? timeoutMs = null)
    {
        return AssertValueWithin(expected, 0, message, timeoutMs);
    }

    /// <inheritdoc />
    public bool WaitValueWithin(double? expected, double tolerance, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() =>
        {
            var actual = GetValue();
            return actual.HasValue && Math.Abs(actual.Value - expected.Value) <= tolerance;
        }, timeout);
    }

    /// <inheritdoc />
    public TScope AssertValueWithin(double? expected, double tolerance, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitValueWithin(expected, tolerance, timeoutMs))
        {
            var actual = GetValue();
            throw new AssertionException(
                message ?? $"Expected value {expected} (±{tolerance}) for '{AutomationId}' but got {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion
}
