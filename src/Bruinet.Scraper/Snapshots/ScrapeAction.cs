namespace Bruinet.Scraper.Snapshots;

/// <summary>
/// Something the person did in the app while a source records actions (<see cref="IScrapeSource.StartActionsAsync"/>):
/// a click, a value, a check. Its element is a node of a capture, so it can be resolved later from files.
/// </summary>
public sealed record ScrapeAction
{
    /// <summary>What it is: one of <see cref="ScrapeActionKind"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>When it happened.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>Where the app was: the page's address, not a frame's.</summary>
    public string Location { get; init; } = "";

    /// <summary>The element's number in the capture taken at <see cref="CapturedAt"/>; null when no capture holds it.</summary>
    public int? Node { get; init; }

    /// <summary>When the capture <see cref="Node"/> is in was taken: its <see cref="ScrapeSnapshot.CapturedAt"/>.</summary>
    public DateTimeOffset? CapturedAt { get; init; }

    /// <summary>
    /// A capture the source took for this action because its element came after the last one: the
    /// recording keeps it before the action.
    /// </summary>
    public ScrapeSnapshot? Capture { get; init; }

    /// <summary>The element from the root down, as it was when the person acted.</summary>
    public IReadOnlyList<ScrapePathStep>? Path { get; init; }

    /// <summary>What was typed, chosen or pressed, or a dialog's text; never a secret's.</summary>
    public string? Value { get; init; }

    /// <summary>For a value typed into a secret field (a password, or one of <see cref="ScrapeActionOptions.Secrets"/>): its placeholder, in place of the value.</summary>
    public string? Secret { get; init; }

    /// <summary>A checkbox's or radio's new state.</summary>
    public bool? Checked { get; init; }

    /// <summary>Whether a dialog was accepted.</summary>
    public bool? Accepted { get; init; }
}

/// <summary>How a source records actions.</summary>
/// <param name="TestIds">The attributes that hold a test id, for an element's path and to find a secret field.</param>
/// <param name="Secrets">Fields whose value never leaves the app; a password field is always one.</param>
public sealed record ScrapeActionOptions(IReadOnlyList<string> TestIds, IReadOnlyList<ScrapeSecret> Secrets);

/// <summary>A field whose value is never recorded: its test id, <c>name</c>, <c>id</c> or label text, and the placeholder recorded instead.</summary>
/// <param name="Field">The field, matched without regard to case.</param>
/// <param name="Placeholder">What is recorded in its place, without braces.</param>
public sealed record ScrapeSecret(string Field, string Placeholder);

/// <summary>The kinds of <see cref="ScrapeAction"/>.</summary>
public static class ScrapeActionKind
{
    /// <summary>A click on an element, or on its nearest clickable ancestor.</summary>
    public const string Click = "click";

    /// <summary>A field's final value after one visit.</summary>
    public const string Input = "input";

    /// <summary>An option chosen: the element is the select (or the list box's owner), the value the option's text.</summary>
    public const string Select = "select";

    /// <summary>A checkbox, switch or radio set to a state.</summary>
    public const string Check = "check";

    /// <summary>Enter, Escape or Tab.</summary>
    public const string Key = "key";

    /// <summary>The page moved to another address.</summary>
    public const string Navigate = "navigate";

    /// <summary>A browser dialog was shown and answered.</summary>
    public const string Dialog = "dialog";

    /// <summary>A check the person picked (<see cref="ScrapeAssert"/>).</summary>
    public const string Assert = "assert";
}

/// <summary>The checks an assert makes (R5).</summary>
public static class ScrapeCheck
{
    /// <summary>The element is visible.</summary>
    public const string Visible = "visible";

    /// <summary>The element has the text.</summary>
    public const string Text = "text";

    /// <summary>The field has the value.</summary>
    public const string Value = "value";

    /// <summary>The checkbox or switch has the state.</summary>
    public const string Checked = "checked";

    /// <summary>The app is on the page.</summary>
    public const string Page = "page";
}

/// <summary>A check the person picked in the app: an element (none for <see cref="ScrapeCheck.Page"/>), what to check, and what it showed.</summary>
/// <param name="Location">Where the app was.</param>
/// <param name="Node">The element's number in the last capture; null for a page check.</param>
/// <param name="Check">One of <see cref="ScrapeCheck"/>.</param>
/// <param name="Value">The text or value it showed, for a text or value check.</param>
/// <param name="Checked">Its state, for a checked check.</param>
public sealed record ScrapeAssert(string Location, int? Node, string Check, string? Value, bool? Checked);

/// <summary>One level of an element's path from the root: what identifies it among its siblings.</summary>
public sealed record ScrapePathStep
{
    /// <summary>The tag name or control type.</summary>
    public string Type { get; init; } = "";

    /// <summary>Its test id, in one of the project's test id attributes.</summary>
    public string? TestId { get; init; }

    /// <summary>Its accessibility role.</summary>
    public string? Role { get; init; }

    /// <summary>Its accessible name.</summary>
    public string? Name { get; init; }

    /// <summary>Its classes, space-separated.</summary>
    public string? Classes { get; init; }
}
