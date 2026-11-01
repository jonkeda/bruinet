namespace Bruinet.Maui.Controls.Display;

/// <summary>
/// MAUI ProgressBar control for displaying progress values (0-1).
/// Provides GetProgress(), IsIndeterminate(), and progress assertions.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ProgressBar<TScope> : Base.ViewBase<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new progress bar control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the progress bar element.</param>
    public ProgressBar(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new progress bar control within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public ProgressBar(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Progress - Core Methods

    /// <summary>
    /// Gets the progress value from pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>Progress value between 0 and 1, or null if not found.</returns>
    protected virtual double? GetProgressCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // The app's own number first, in MAUI's units.
        if (double.TryParse(
                element.ReadState("Progress"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var reported))
        {
            return reported;
        }

        // A progress bar reports its value through the range pattern, and reports it in its own
        // units: WinUI uses 0-100 where MAUI's Progress is 0-1. Normalising against the reported
        // minimum and maximum is what makes the returned value mean the same thing everywhere.
        if (element.RangeValue is { } current)
        {
            var min = element.RangeMinimum ?? 0d;
            var max = element.RangeMaximum ?? 1d;
            return max > min ? (current - min) / (max - min) : current;
        }

        // Nothing else to ask: a progress bar without the range pattern publishes no progress.
        return null;
    }

    #endregion

    #region Indeterminate - Core Methods


    #endregion

    #region Hand-written Convenience Members

    // Tolerance-based comparisons stay hand-written; they overload the generated
    // exact-equality AssertProgress/WaitProgress.

    /// <summary>
    /// Waits for the progress value to reach the expected value within a tolerance.
    /// </summary>
    /// <param name="expected">The expected progress value (0-1). Null skips the wait.</param>
    /// <param name="tolerance">Allowed tolerance for comparison.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    /// <returns>True if the condition was met, false if the timeout was reached.</returns>
    public bool WaitProgress(double? expected, double tolerance, int? timeoutMs = null)
    {
        if (expected == null)
            return true;

        return RunWaitWithElement(expected,
            e =>
            {
                var actual = GetProgressCore(e);
                return actual.HasValue && Math.Abs(actual.Value - expected.Value) <= tolerance;
            },
            timeoutMs);
    }

    /// <summary>
    /// Asserts progress has reached expected value within a tolerance.
    /// </summary>
    /// <param name="expected">The expected progress value (0-1). Null skips the assertion.</param>
    /// <param name="tolerance">Allowed tolerance for comparison.</param>
    /// <param name="message">Optional assertion message.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    public TScope AssertProgress(double? expected, double tolerance, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;

        return RunAssertWithElement(expected,
            GetProgressCore,
            (actual, exp) => actual.HasValue && exp.HasValue && Math.Abs(actual.Value - exp.Value) <= tolerance,
            message ?? $"Expected progress {expected} (±{tolerance}). Locator: {Locator}", timeoutMs);
    }

    #endregion
}
