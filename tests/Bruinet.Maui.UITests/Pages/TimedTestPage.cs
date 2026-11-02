namespace Bruinet.Maui.UITests.Pages;

/// <summary>The timed sample page: UI that changes after a delay, a debounce, a message that hides.</summary>
public class TimedTestPage : PageObjectBase<TimedTestPage>
{
    public TimedTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "TimedPage";

    public Button<TimedTestPage> SaveButton => new(this, "SaveButton");

    /// <summary>"saving", then "saved" after an awaited delay.</summary>
    public Label<TimedTestPage> SaveStatus => new(this, "SaveStatus");

    public Button<TimedTestPage> SaveLaterButton => new(this, "SaveLaterButton");

    /// <summary>"saving", then "saved" by work dispatched with a delay.</summary>
    public Label<TimedTestPage> SaveLaterStatus => new(this, "SaveLaterStatus");

    /// <summary>A search that runs once typing pauses.</summary>
    public Entry<TimedTestPage> Search => new(this, "DebouncedSearch");

    /// <summary>"searched n: text".</summary>
    public Label<TimedTestPage> SearchStatus => new(this, "SearchStatus");

    public Button<TimedTestPage> ShowMessageButton => new(this, "ShowMessageButton");

    /// <summary>Shown for a second.</summary>
    public Label<TimedTestPage> Message => new(this, "TimedMessage");
}
