using Bruinet.Bdd;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Testing;
using Bruinet.Html.Controls;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;

namespace Bruinet.Samples.Todo.Web.UITests.Pages;

/// <summary>
/// The status component (web <c>TodoStatusView</c>): a container whose parts' ids repeat in every
/// row, so they are found within it. The web version of the MAUI <c>TodoStatus</c> ControlObject,
/// with its members and steps.
/// </summary>
public sealed class TodoStatus<TParent>(IHtmlScope<TParent> parent, string testId)
    : ContainerBase<TParent, TodoStatus<TParent>>(parent, Locator.ByDataTestId(testId))
    where TParent : IHtmlScope<TParent>
{
    public override TodoStatus<TParent> Self => this;

    /// <summary>The status name, as shown ("Open", "In progress", "Done", "Overdue").</summary>
    public LabelControl<TodoStatus<TParent>> Text => new(this, Locator.ByDataTestId("StatusText"));

    /// <summary>Next: cycles the status. Absent where the control is read-only.</summary>
    public ButtonControl<TodoStatus<TParent>> Next => new(this, Locator.ByDataTestId("StatusNext"));

    /// <summary>The status name shown.</summary>
    public string? GetStatus(int? timeoutMs = null) => Text.GetText(timeoutMs);

    /// <summary>Waits until the status name shown is <paramref name="expected"/>.</summary>
    public bool WaitStatus(string? expected, int? timeoutMs = null) => Text.WaitText(expected, timeoutMs);

    /// <summary>Asserts the status shown, waiting for it.</summary>
    public TParent AssertStatus(string expected, int? timeoutMs = null)
    {
        Text.AssertText(expected, timeoutMs: timeoutMs);
        return Parent;
    }

    /// <summary>Asserts the status shown, waiting for it: the MAUI step.</summary>
    [BddThen("{control} should show status {value}")]
    public TodoStatus<TParent> AssertShowsStatus(string? expected, string? message = null, int? timeoutMs = null)
    {
        Text.AssertText(expected, message, timeoutMs);
        return this;
    }

    /// <summary>Asserts whether the status can be changed here: whether it has a Next button.</summary>
    public TodoStatus<TParent> AssertChangeable(bool expected = true, int? timeoutMs = null)
    {
        Next.AssertExists(expected, timeoutMs: timeoutMs);
        return this;
    }

    /// <summary>Presses Next until the status reads <paramref name="expected"/>, at most one full cycle.</summary>
    [BddWhen("I advance {control} to {value}")]
    public TodoStatus<TParent> AdvanceTo(string? expected, int? timeoutMs = null)
    {
        if (expected is null)
        {
            return this;
        }

        // A press that lands on another name costs this much; the last press gets the full timeout.
        const int intermediateWaitMs = 750;

        for (var press = 0; press < 3 && Text.TryGetText() != expected; press++)
        {
            Next.Click(timeoutMs);
            WaitStatus(expected, press == 2 ? timeoutMs : intermediateWaitMs);
        }

        if (Text.TryGetText() is var shown && shown != expected)
        {
            throw new TimeoutException(
                $"TodoStatus '{Locator}' never read '{expected}' in a full cycle; it reads '{shown}'.");
        }

        return this;
    }
}

/// <summary>
/// The list's state (<c>TodoList_State</c>): exactly one of Loading, Empty, Error or the rows shows,
/// each with its own id, as in the MAUI <c>StateContainer</c>.
/// </summary>
/// <remarks>
/// It does not ask its page: the page is busy while a sync runs, and Loading is what a sync shows.
/// </remarks>
public sealed class TodoListState(IHtmlScope<TodoListPage> page)
    : ContainerBase<TodoListPage, TodoListState>(page, Locator.ByDataTestId("TodoList_State"))
{
    public const string Loading = "TodoList_Loading";

    public const string Empty = "TodoList_Empty";

    public const string Error = "TodoList_Error";

    public const string Content = "TodoList_Items";

    public override TodoListState Self => this;

    protected override bool AsksParent => false;

    /// <summary>Whether the state with test id <paramref name="state"/> is the one shown now.</summary>
    public bool IsShowing(string state) => TryFindElement(Locator.ByDataTestId(state)) != null;

    /// <summary>Waits until the state with test id <paramref name="state"/> is shown, or is not.</summary>
    public bool WaitShowing(string state, bool expected = true, int? timeoutMs = null)
        => new ShownPart(this, state).WaitExists(expected, timeoutMs);

    /// <summary>Asserts that the state with test id <paramref name="state"/> is shown, waiting for it.</summary>
    public TodoListState AssertShowing(string state, bool expected = true, int? timeoutMs = null)
    {
        if (!WaitShowing(state, expected, timeoutMs))
        {
            throw new ControlAssertionException($"Expected the list {(expected ? "to show" : "not to show")} {state}.");
        }

        return this;
    }

    /// <summary>Asserts that the named state (Loading, Empty, Error or Content) is shown, waiting for it: the MAUI step.</summary>
    [BddThen("{control} should show the {value} state")]
    public TodoListState AssertShowsState(string state, int? timeoutMs = null) => AssertShowing(
        state switch
        {
            nameof(Loading) => Loading,
            nameof(Empty) => Empty,
            nameof(Error) => Error,
            nameof(Content) => Content,
            _ => throw new ArgumentException($"Unknown list state '{state}'. Use Loading, Empty, Error or Content.", nameof(state)),
        },
        timeoutMs: timeoutMs);

    // One state's element, as a container of the state: its presence is the question.
    private sealed class ShownPart(IHtmlScope<TodoListState> state, string testId)
        : ContainerBase<TodoListState, ShownPart>(state, Locator.ByDataTestId(testId))
    {
        public override ShownPart Self => this;

        protected override bool AsksParent => false;
    }
}

/// <summary>A card with a header: controls inside it are found within the card.</summary>
public sealed class SectionCard<TParent>(IHtmlScope<TParent> parent, string testId)
    : ContainerBase<TParent, SectionCard<TParent>>(parent, Locator.ByDataTestId(testId))
    where TParent : IHtmlScope<TParent>
{
    public override SectionCard<TParent> Self => this;

    public LabelControl<SectionCard<TParent>> Header => new(this, Locator.ByDataTestId("SectionHeader"));

    /// <summary>A value label inside the card, by its test id.</summary>
    public LabelControl<SectionCard<TParent>> Value(string testId) => new(this, Locator.ByDataTestId(testId));
}

/// <summary>
/// The web head's alert, shown over its page. It does not ask its page: the page is busy while
/// the command that opened the dialog waits for its answer (design 7.2, MAUI's A3).
/// </summary>
public sealed class TodoDialog<TParent>(IHtmlScope<TParent> parent)
    : ContainerBase<TParent, TodoDialog<TParent>>(parent, Locator.ByDataTestId("Dialog"))
    where TParent : IHtmlScope<TParent>
{
    public override TodoDialog<TParent> Self => this;

    protected override bool AsksParent => false;

    public LabelControl<TodoDialog<TParent>> Title => new(this, Locator.ByDataTestId("Dialog_Title"));

    public ButtonControl<TodoDialog<TParent>> Accept => new(this, Locator.ByDataTestId("Dialog_Accept"));

    public ButtonControl<TodoDialog<TParent>> Cancel => new(this, Locator.ByDataTestId("Dialog_Cancel"));

    /// <summary>The dialog's button whose text is <paramref name="text"/>, as the MAUI dialog finds its buttons.</summary>
    /// <exception cref="ElementNotFoundException">Neither button reads <paramref name="text"/>.</exception>
    public ButtonControl<TodoDialog<TParent>> Button(string text)
    {
        AssertExists(true);
        foreach (var button in new[] { Accept, Cancel })
        {
            if (string.Equals(button.TryGetText()?.Trim(), text, StringComparison.Ordinal))
            {
                return button;
            }
        }

        throw new ElementNotFoundException(
            $"The dialog has no button '{text}'. It has '{Accept.TryGetText()}' and '{Cancel.TryGetText()}'.");
    }

    /// <summary>Asserts that the dialog is shown with this title, waiting for it: the MAUI step.</summary>
    [BddThen("{control} should ask {value}")]
    public TodoDialog<TParent> AssertAsks(string title, int? timeoutMs = null)
    {
        AssertExists(true, timeoutMs: timeoutMs);
        Title.AssertText(title, timeoutMs: timeoutMs);
        return this;
    }

    /// <summary>Presses the dialog's button with this text: the MAUI step.</summary>
    [BddWhen("I choose {value} in {control}", Navigates = true)]
    [BddWhen("{user} chooses {value} in {control}", Navigates = true)]
    public TodoDialog<TParent> Choose(string text)
    {
        Button(text).Click();
        return this;
    }

    /// <summary>Asserts that no dialog is shown, waiting for it to close: the MAUI step.</summary>
    [BddThen("no {control} should be shown")]
    public TodoDialog<TParent> AssertNotShown(int? timeoutMs = null) => AssertExists(false, timeoutMs: timeoutMs);
}
