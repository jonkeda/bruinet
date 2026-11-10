using System.Globalization;
using Bruinet.Scraper.Snapshots;
using System.Text.Json.Serialization;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// One thing that happened in a recording session: a page capture, or an action the person took in a
/// test recording.
/// </summary>
public sealed record RecordingEvent
{
    /// <summary>The kind of a page capture.</summary>
    public const string PageKind = "page";

    /// <summary>A click on <see cref="Node"/>.</summary>
    public const string ClickKind = ScrapeActionKind.Click;

    /// <summary>A field's final <see cref="Value"/> after one visit.</summary>
    public const string InputKind = ScrapeActionKind.Input;

    /// <summary>An option chosen in a select or list box: <see cref="Node"/> is the select, <see cref="Value"/> the option's text.</summary>
    public const string SelectKind = ScrapeActionKind.Select;

    /// <summary>A checkbox or radio set to <see cref="Checked"/>.</summary>
    public const string CheckKind = ScrapeActionKind.Check;

    /// <summary>Enter, Escape or Tab, in <see cref="Value"/>; other keys are part of an input.</summary>
    public const string KeyKind = ScrapeActionKind.Key;

    /// <summary>The app went to <see cref="Location"/>; the page capture follows it.</summary>
    public const string NavigateKind = ScrapeActionKind.Navigate;

    /// <summary>A browser dialog with the text <see cref="Value"/>, <see cref="Accepted"/> or dismissed.</summary>
    public const string DialogKind = ScrapeActionKind.Dialog;

    /// <summary>A check the person picked: <see cref="Check"/> on <see cref="Node"/>, expecting <see cref="Value"/>.</summary>
    public const string AssertKind = ScrapeActionKind.Assert;

    /// <summary>Every kind of action.</summary>
    public static IReadOnlySet<string> ActionKinds { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ClickKind, InputKind, SelectKind, CheckKind, KeyKind, NavigateKind, DialogKind, AssertKind,
    };

    /// <summary>The actions on an element, which name a <see cref="Node"/> or a <see cref="Path"/>.</summary>
    public static IReadOnlySet<string> ElementKinds { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ClickKind, InputKind, SelectKind, CheckKind,
    };

    /// <summary>Its number in the session, from 1.</summary>
    public int Number { get; init; }

    /// <summary>What it is: <see cref="PageKind"/> or one of <see cref="ActionKinds"/>.</summary>
    public string Kind { get; init; } = PageKind;

    /// <summary>When it happened.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>Where the app was.</summary>
    public string Location { get; init; } = "";

    /// <summary>The page's key (<see cref="PageKey"/>).</summary>
    public string Page { get; init; } = "";

    /// <summary>The page's title.</summary>
    public string Title { get; init; } = "";

    /// <summary>What the user called the page, if anything.</summary>
    public string? Name { get; init; }

    /// <summary>The snapshot file, in the session folder.</summary>
    public string? Snapshot { get; init; }

    /// <summary>The screenshot file, in the session folder.</summary>
    public string? Screenshot { get; init; }

    /// <summary>How many nodes the snapshot has.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Nodes { get; init; }

    /// <summary>
    /// An action's element: its number in its capture (<see cref="RecordingSession.CaptureOf"/>), or null
    /// when no capture holds it and only <see cref="Path"/> describes it.
    /// </summary>
    public int? Node { get; init; }

    /// <summary>The page event <see cref="Node"/> is in; null for the latest capture of <see cref="Page"/> before it.</summary>
    public int? Capture { get; init; }

    /// <summary>An action's element from the root down, as it was when the person acted.</summary>
    public IReadOnlyList<ScrapePathStep>? Path { get; init; }

    /// <summary>What was typed, chosen, pressed or expected, or a dialog's text; masked, and never a secret's.</summary>
    public string? Value { get; init; }

    /// <summary>
    /// For a value typed into a secret field: the placeholder recorded instead (R7), which the person maps
    /// to a setting or test data.
    /// </summary>
    public string? Placeholder { get; init; }

    /// <summary>A check action's new state, or the state an assert expects.</summary>
    public bool? Checked { get; init; }

    /// <summary>An assert's check: one of <see cref="AssertCheck"/>.</summary>
    public string? Check { get; init; }

    /// <summary>Whether a dialog was accepted.</summary>
    public bool? Accepted { get; init; }

    /// <summary>True for an action, false for a page capture.</summary>
    [JsonIgnore]
    public bool IsAction => ActionKinds.Contains(Kind);

    /// <summary>The name to show: the user's, else the title, else the page key.</summary>
    [JsonIgnore]
    public string DisplayName => Name ?? (Title.Length > 0 ? Title : Page);

    /// <inheritdoc/>
    public override string ToString() => Node is { } node
        ? string.Create(CultureInfo.InvariantCulture, $"{Number} {Kind} {Page} #{node}")
        : string.Create(CultureInfo.InvariantCulture, $"{Number} {Kind} {Page}");
}

/// <summary>The checks an assert can make (R5).</summary>
public static class AssertCheck
{
    /// <summary>The element is visible.</summary>
    public const string Visible = ScrapeCheck.Visible;

    /// <summary>The element has the text <see cref="RecordingEvent.Value"/>.</summary>
    public const string Text = ScrapeCheck.Text;

    /// <summary>The field has the value <see cref="RecordingEvent.Value"/>.</summary>
    public const string Value = ScrapeCheck.Value;

    /// <summary>The checkbox is <see cref="RecordingEvent.Checked"/>.</summary>
    public const string Checked = ScrapeCheck.Checked;

    /// <summary>The app is on the page <see cref="RecordingEvent.Page"/>.</summary>
    public const string Page = ScrapeCheck.Page;

    /// <summary>Every check.</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal) { Visible, Text, Value, Checked, Page };
}
