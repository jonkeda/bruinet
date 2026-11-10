using Bruinet.Bdd;
using Bruinet.Core.Testing;

namespace Bruinet.Samples.Todo.UITests.Controls;

/// <summary>
/// The Todo app's status control (<c>TodoStatusView</c>): a glyph, the status name, and a Next
/// button that cycles Open, In progress, Done.
/// </summary>
/// <remarks>
/// <para>
/// <b>A component.</b> The app gives the control its AutomationId; the control fixes its parts'
/// ids itself (<c>StatusGlyph</c>, <c>StatusText</c>, <c>StatusNext</c>). Those ids repeat in every
/// list row, so they are only unique inside the component's root, which is what the component
/// scopes to.
/// </para>
/// <para>
/// <b>Route, Windows:</b> the root is a group (the app registers the Bruinet automation handlers,
/// without which a <c>ContentView</c> publishes no AutomationId), the glyph and name are text, and
/// Next answers <c>InvokePattern</c>. No bridge (AD-008, test 1). Probed 2026-09-18: the list row
/// root reads <c>Status: Overdue</c> with the two text parts and no button; the detail page's has
/// all three.
/// </para>
/// <para>
/// <b>Overdue is what the user sees,</b> not a status: a todo past its due date shows Overdue until
/// it is Done. Cycling a past-due todo therefore reads Overdue, Overdue, Done.
/// </para>
/// </remarks>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TodoStatus<TScope> : ComponentObjectBase<TScope, TodoStatus<TScope>>
    where TScope : IMauiScope<TScope>
{
    private const string GlyphId = "StatusGlyph";
    private const string TextId = "StatusText";
    private const string NextId = "StatusNext";

    /// <summary>Creates the status control within the specified scope.</summary>
    /// <param name="scope">The page, container or row holding it.</param>
    /// <param name="locator">The locator for the control's root.</param>
    public TodoStatus(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>Creates the status control within the specified scope.</summary>
    /// <param name="scope">The page, container or row holding it.</param>
    /// <param name="locatorValue">The root's AutomationId.</param>
    public TodoStatus(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Parts

    /// <summary>The glyph: ○ open, ◐ in progress, ● done, ⚠ overdue.</summary>
    public Label<TodoStatus<TScope>> Glyph => new(this, GlyphId);

    /// <summary>The status name the user reads.</summary>
    public Label<TodoStatus<TScope>> Text => new(this, TextId);

    /// <summary>The Next button; absent where the control is read-only (list rows).</summary>
    public TodoStatusNextButton<TodoStatus<TScope>> Next => new(this, NextId);

    #endregion

    #region Shortcuts

    /// <summary>The status name shown: Open, In progress, Done or Overdue.</summary>
    [GenerateComparisons(Comparison.Equals)]
    protected string? GetStatusShortcut() => Text.GetText();

    /// <summary>The glyph shown.</summary>
    [GenerateComparisons(Comparison.Equals)]
    protected string? GetGlyphShortcut() => Glyph.GetText();

    /// <summary>Whether the Next button is shown, which is whether the status can be changed here.</summary>
    protected bool? IsChangeableShortcut() => Next.IsShown();

    #endregion

    #region Hand-written: across parts

    /// <summary>
    /// Presses Next until the status name reads <paramref name="expected"/> (see <see cref="StatusText"/>), at most three times.
    /// </summary>
    /// <remarks>
    /// Across two parts: the Next button, and the name that shows the result; no single part knows
    /// both. Null skips; already there does nothing. Three presses go round the whole cycle, so a
    /// name never reached is not one this todo can show (Open or In progress on a past-due todo,
    /// which read Overdue). Each press is a synchronous Invoke, so a press that lands on another name
    /// cannot be overtaken by the next.
    /// </remarks>
    /// <param name="expected">Open, In progress, Done or Overdue.</param>
    /// <param name="timeoutMs">How long to wait after each press.</param>
    [BddWhen("I advance {control} to {value}")]
    public TodoStatus<TScope> AdvanceTo(string? expected, int? timeoutMs = null)
    {
        if (expected is null)
        {
            return this;
        }

        // A press that lands on another name costs this much; the last press gets the full timeout.
        const int intermediateWaitMs = 750;

        for (var press = 0; press < 3 && GetStatus() != expected; press++)
        {
            Next.Click(timeoutMs);
            WaitStatus(expected, press == 2 ? timeoutMs : intermediateWaitMs);
        }

        if (GetStatus() != expected)
        {
            throw new TimeoutException(
                $"TodoStatus '{Locator.Value}' never read '{expected}' in a full cycle; it reads '{GetStatus()}'. Locator: {Locator}");
        }

        return this;
    }

    /// <summary>Asserts the status name shown (see <see cref="AssertStatus"/>).</summary>
    /// <remarks>The UAT verb <c>{control} should show status {value}</c>; delegates to the generated assert.</remarks>
    /// <param name="expected">Open, In progress, Done or Overdue.</param>
    /// <param name="message">An optional message for the failure.</param>
    /// <param name="timeoutMs">How long to wait for the name.</param>
    [BddThen("{control} should show status {value}")]
    public TodoStatus<TScope> AssertShowsStatus(string? expected, string? message = null, int? timeoutMs = null)
        => AssertStatus(expected, message, timeoutMs);

    #endregion
}
