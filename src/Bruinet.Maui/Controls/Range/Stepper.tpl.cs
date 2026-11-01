using Bruinet.Core.Calls;
using Bruinet.Core.Locators;
using Bruinet.Maui.Controls.Base;
using System.Globalization;

namespace Bruinet.Maui.Controls.Range;

/// <summary>
/// MAUI Stepper control with +/- buttons for discrete value changes.
/// Inherits GetValue, SetValue, GetMinimum, GetMaximum, Increment, Decrement from RangeControlBase.
/// Provides additional stepper-specific methods like IncrementBy and DecrementBy.
/// Overrides Increment/Decrement to invoke the stepper's own buttons instead of sending keys.
///
/// Windows MAUI Note: On Windows, MAUI Stepper doesn't expose a single element with the AutomationId.
/// Instead, it exposes separate button elements with "{AutomationId}Minus" and "{AutomationId}Plus".
/// This control resolves through that pair, presses them through the Invoke pattern, and reads
/// Value, Minimum, Maximum and Increment from the app with the bridge's GetState verb, which the
/// Stepper must declare.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Stepper<TScope> : Base.RangeControlBase<TScope>
    where TScope : IMauiScope<TScope>
{
    private readonly string? _baseAutomationId;

    private sealed record StepperParts(
        IMauiElement Proxy,
        IMauiElement? Native,
        IMauiElement? Minus,
        IMauiElement? Plus)
    {
        public bool IsButtonMode => Native is null;
    }

    /// <summary>
    /// Creates a new stepper control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the stepper element.</param>
    public Stepper(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
        // Extract base automation ID for button mode fallback
        if (locator.Strategy == LocatorStrategy.AutomationId)
        {
            _baseAutomationId = locator.Value;
        }
    }

    /// <summary>
    /// Creates a new stepper control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public Stepper(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
        // Store base automation ID for button mode fallback
        _baseAutomationId = locatorValue;
    }

    #region Button Mode for Windows

    /// <summary>
    /// Resolves the native Stepper or its two Windows buttons.
    /// </summary>
    private StepperParts? ResolveParts()
    {
        var element = base.TryFindElement();
        if (element != null)
        {
            return new StepperParts(element, element, null, null);
        }

        if (string.IsNullOrEmpty(_baseAutomationId))
        {
            return null;
        }

        var minus = MauiScope.TryFindElement(
            new Locator(LocatorStrategy.AutomationId, $"{_baseAutomationId}Minus"));
        var plus = MauiScope.TryFindElement(
            new Locator(LocatorStrategy.AutomationId, $"{_baseAutomationId}Plus"));

        return minus is not null && plus is not null
            ? new StepperParts(minus, null, minus, plus)
            : null;
    }

    /// <summary>
    /// Tries to find the stepper element. On Windows, the minus button is its proxy.
    /// </summary>
    protected override IMauiElement? TryFindElement()
    {
        return ResolveParts()?.Proxy;
    }

    /// <summary>
    /// Says where the stepper was looked for: its own locator, then its two buttons.
    /// </summary>
    protected override ElementNotFoundException NotFound()
        => new($"Stepper was not found by '{Locator}', '{_baseAutomationId}Minus', or "
               + $"'{_baseAutomationId}Plus'.");

    /// <summary>
    /// The Stepper as the app declared it, or null where it was never declared.
    /// </summary>
    private IMauiElement? StateSource()
        => string.IsNullOrEmpty(_baseAutomationId)
            ? null
            : Context.AppElement.TryFindDeclared(_baseAutomationId);

    /// <summary>
    /// A number the app publishes for this Stepper, or null where it publishes none.
    /// </summary>
    private double? ReadNumericState(string property)
    {
        if (StateSource()?.ReadState(property) is not { } value)
        {
            return null;
        }

        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : throw new FormatException(
                $"Stepper '{_baseAutomationId}' returned '{value}' for {property}.");
    }

    #endregion

    #region Core Method Overrides

    /// <summary>
    /// Presses the stepper's + button: the Windows <c>{id}Plus</c> button, or the last button
    /// inside the Stepper elsewhere.
    /// </summary>
    /// <param name="element">The pre-found stepper element.</param>
    /// <param name="timeoutMs">Unused: a press is one action, and its effect is the caller's to confirm.</param>
    protected override void IncrementCore(IMauiElement element, int? timeoutMs = null)
        => PressButton(element, isIncrement: true);

    /// <summary>
    /// Presses the stepper's - button: the Windows <c>{id}Minus</c> button, or the first button
    /// inside the Stepper elsewhere.
    /// </summary>
    /// <param name="element">The pre-found stepper element.</param>
    /// <param name="timeoutMs">Unused: a press is one action, and its effect is the caller's to confirm.</param>
    protected override void DecrementCore(IMauiElement element, int? timeoutMs = null)
        => PressButton(element, isIncrement: false);

    private void PressButton(IMauiElement element, bool isIncrement)
    {
        var parts = ResolveParts();
        var button = parts?.IsButtonMode == true
            ? (isIncrement ? parts.Plus : parts.Minus)
            : FindChildButton(element, isIncrement);

        if (button is null)
        {
            throw new NotSupportedException(
                $"Stepper '{StepperName}' has no {(isIncrement ? "+" : "-")} button to press.");
        }

        button.Invoke();
    }

    /// <summary>
    /// Gets the current value: from the app's GetState when the Stepper declares it, otherwise
    /// from the value the element publishes as a range.
    /// </summary>
    /// <param name="element">The stepper element (or proxy button in button mode).</param>
    /// <returns>The current value, or null if not available.</returns>
    protected override double? GetValueCore(IMauiElement? element)
    {
        return ReadNumericState("Value") ?? base.GetValueCore(element);
    }

    /// <inheritdoc />
    protected override double? GetMinimumCore(IMauiElement? element)
    {
        return ReadNumericState("Minimum") ?? base.GetMinimumCore(element);
    }

    /// <inheritdoc />
    protected override double? GetMaximumCore(IMauiElement? element)
    {
        return ReadNumericState("Maximum") ?? base.GetMaximumCore(element);
    }

    /// <inheritdoc />
    protected override double? GetStepCore(IMauiElement? element)
    {
        return ReadNumericState("Increment") ?? base.GetStepCore(element);
    }

    /// <summary>How close two values must be to count as the same.</summary>
    private const double ValueTolerance = 0.01;

    /// <summary>
    /// Sets the value the way a user does: press towards the target, watch the value move, and
    /// repeat.
    /// </summary>
    /// <param name="element">The pre-found stepper element.</param>
    /// <param name="value">The target value. Null skips the operation.</param>
    /// <param name="timeoutMs">The budget for all the presses and their confirmations; null for the default.</param>
    protected override void SetValueCore(IMauiElement element, double? value, int? timeoutMs = null)
    {
        if (value == null)
            return;

        EnsureSettableCore(element);

        var minimum = GetMinimumCore(element);
        var maximum = GetMaximumCore(element);
        var target = Math.Clamp(
            value.Value,
            minimum ?? double.NegativeInfinity,
            maximum ?? double.PositiveInfinity);
        var current = GetValueCore(element) ?? throw NoValue();
        var deadline = Deadline.In(timeoutMs ?? DefaultTimeoutMs);
        double? step = null;
        var presses = 0;

        while (Math.Abs(target - current) > ValueTolerance
               && (step is null || Math.Abs(target - current) >= step.Value / 2 - ValueTolerance))
        {
            var up = target > current;
            var before = current;

            PressButton(element, up);
            presses++;

            var confirmation = Confirm(
                () => GetValueCore(element),
                actual => actual.HasValue
                    && (up ? actual.Value > before + ValueTolerance : actual.Value < before - ValueTolerance),
                deadline.RemainingMs);

            if (!confirmation.IsConfirmed)
            {
                var boundUnpublished = (up ? maximum : minimum) is null;
                if (confirmation.Result == ConfirmationResult.NotConfirmed && step is not null && boundUnpublished)
                {
                    return;
                }

                var press = up ? "+" : "-";
                throw confirmation.Failure(Locator, $"the {press} press", lastError => new TimeoutException(
                    $"Stepper '{StepperName}' stayed at {before} after press {presses} ({press}) towards {target}, "
                    + $"for {deadline.ElapsedMs} ms.",
                    lastError));
            }

            current = confirmation.LastValue!.Value;
            step ??= Math.Abs(current - before);
        }
    }

    /// <summary>The Stepper's name for messages: its AutomationId, or its locator's value.</summary>
    private string StepperName => _baseAutomationId ?? Locator.Value;

    /// <summary>The failure when the Stepper publishes no value to press towards.</summary>
    private NotSupportedException NoValue()
        => new($"Stepper '{StepperName}' does not expose its current value. On Windows the app declares "
               + "the GetState verb on it; on Android the app includes Bruinet.Maui.AppSupport "
               + "(AddBruinetAutomationHandlers), which publishes it as the node's range.");

    /// <summary>
    /// Finds the increment or decrement button inside the Stepper.
    /// </summary>
    /// <param name="parent">The parent stepper element.</param>
    /// <param name="isIncrement">True for increment button, false for decrement.</param>
    /// <returns>The button element, or null if not found.</returns>
    private IMauiElement? FindChildButton(IMauiElement parent, bool isIncrement)
    {
        foreach (var locator in new[] { Locator.ByClassName("RepeatButton"), Locator.ByControlType("button") })
        {
            var buttons = parent.FindElements(locator);
            if (buttons.Count >= 2)
            {
                return isIncrement ? buttons[^1] : buttons[0];
            }
        }

        return null;
    }

    #endregion

    #region Stepper-Specific Core Methods

    /// <summary>
    /// Increments the stepper value multiple times.
    /// </summary>
    /// <param name="element">The pre-found stepper element.</param>
    /// <param name="times">Number of times to increment. Null or non-positive skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout for each increment.</param>
    protected virtual void IncrementByCore(IMauiElement element, int? times, int? timeoutMs = null)
    {
        if (times == null || times <= 0)
            return;

        for (int i = 0; i < times; i++)
        {
            IncrementCore(element, timeoutMs);
        }
    }

    /// <summary>
    /// Decrements the stepper value multiple times.
    /// </summary>
    /// <param name="element">The pre-found stepper element.</param>
    /// <param name="times">Number of times to decrement. Null or non-positive skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout for each decrement.</param>
    protected virtual void DecrementByCore(IMauiElement element, int? times, int? timeoutMs = null)
    {
        if (times == null || times <= 0)
            return;

        for (int i = 0; i < times; i++)
        {
            DecrementCore(element, timeoutMs);
        }
    }

    /// <summary>
    /// Sets the stepper to its minimum value.
    /// </summary>
    /// <param name="element">The pre-found stepper element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetToMinimumCore(IMauiElement element, int? timeoutMs = null)
    {
        var min = GetMinimumCore(element) ?? 0;
        SetValueCore(element, min, timeoutMs);
    }

    /// <summary>
    /// Sets the stepper to its maximum value.
    /// </summary>
    /// <param name="element">The pre-found stepper element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetToMaximumCore(IMauiElement element, int? timeoutMs = null)
    {
        var max = GetMaximumCore(element) ?? 100;
        SetValueCore(element, max, timeoutMs);
    }

    #endregion

    #region Hand-written Convenience Members

    /// <summary>
    /// Checks if the stepper can be incremented (not at maximum).
    /// </summary>
    /// <returns>True if increment is possible, false otherwise.</returns>
    public bool? CanIncrement()
    {
        var element = TryFindElement();
        if (element == null)
            return null;

        var current = GetValueCore(element);
        var max = GetMaximumCore(element);

        if (current == null || max == null)
            return null;
        return current.Value < max.Value;
    }

    /// <summary>
    /// Checks if the stepper can be decremented (not at minimum).
    /// </summary>
    /// <returns>True if decrement is possible, false otherwise.</returns>
    public bool? CanDecrement()
    {
        var element = TryFindElement();
        if (element == null)
            return null;

        var current = GetValueCore(element);
        var min = GetMinimumCore(element);

        if (current == null || min == null)
            return null;
        return current.Value > min.Value;
    }

    #endregion
}
