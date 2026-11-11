using System.Diagnostics;
using Bruinet.Core.Calls;
using Bruinet.Core.Logging;
using Bruinet.Core.Scopes;

namespace Bruinet.Core.Tests;

/// <summary>
/// The call model on a fake element, once for every stack (review 08, finding 4): what WPF, WinForms and native
/// Android each proved again on their own fakes. A stack's own file keeps only what that stack adds.
/// </summary>
public sealed class CallModelTests
{
    private readonly FakeHost _host = new();

    [Fact]
    public void An_Action_FindsTheElementAgain_WhenTheFirstOneNeverShows()
    {
        var hidden = new FakeElement { Visible = false };
        var shown = new FakeElement();
        var lookups = 0;
        _host.Next = () => ++lookups == 1 ? hidden : shown;

        _host.Calls.Act(element => element.Clicks++, ensureVisible: true, timeoutMs: 1000, caller: "Click");

        Assert.Equal((0, 1), (hidden.Clicks, shown.Clicks));
    }

    [Fact]
    public void A_Read_HonoursTheCallersTimeout_WhenTheElementNeverShows()
    {
        _host.Next = () => new FakeElement { Visible = false };

        var elapsed = Timed(() => _host.Calls.GetWithElement(element => element.Text, timeoutMs: 300, caller: "GetText"));

        Assert.True(elapsed < 800, $"GetText(timeoutMs: 300) took {elapsed} ms.");
    }

    [Fact]
    public void A_Call_WritesOneLogEntryAndExitPair()
    {
        _host.Calls.Act(element => element.Clicks++, ensureVisible: true, timeoutMs: 1000, caller: "Click");

        Assert.Equal(1, _host.Logger.Events.Count(e => e.Kind == LogEventKind.Entry));
        Assert.Equal(1, _host.Logger.Events.Count(e => e.Kind == LogEventKind.Exit));
    }

    [Fact]
    public void An_Action_WaitsForADisabledControl_AndActsOnce()
    {
        var reads = 0;
        var button = new FakeElement { Enabled = () => ++reads > 2 };
        _host.Next = () => button;

        _host.Calls.Act(element => element.Clicks++, ensureVisible: true, timeoutMs: 1000, caller: "Click");

        Assert.Equal(1, button.Clicks);
    }

    [Fact]
    public void A_Call_OnAnAppThatExited_ThrowsAppUnavailable_WithoutWaitingOutItsBudget()
    {
        _host.Scope = () => throw new AppUnavailableException("the application process has exited.");

        var elapsed = Timed(() => Assert.ThrowsAny<AppUnavailableException>(
            () => _host.Calls.Act(element => element.Clicks++, ensureVisible: true, timeoutMs: 5000, caller: "Click")));

        Assert.True(elapsed < 2000, $"The call took {elapsed} ms on an app that had exited.");
    }

    [Fact]
    public void A_Control_OnAPageThatIsNotShown_FailsAfterItsBudget_WithoutActing()
    {
        var button = new FakeElement();
        _host.Next = () => button;
        _host.Scope = () => new ScopeReadiness("Page", ScopeReadinessState.NotLoaded, "'Page' is not shown");

        Assert.ThrowsAny<BruinetException>(
            () => _host.Calls.Act(element => element.Clicks++, ensureVisible: true, timeoutMs: 300, caller: "Click"));

        Assert.Equal(0, button.Clicks);
    }

    private static long Timed(Action act)
    {
        var stopwatch = Stopwatch.StartNew();
        _ = Record.Exception(act);
        return stopwatch.ElapsedMilliseconds;
    }
}

/// <summary>A control's element as the call model sees it.</summary>
public sealed class FakeElement
{
    public bool Visible { get; set; } = true;

    public Func<bool> Enabled { get; set; } = () => true;

    public string? Text { get; set; } = "text";

    public int Clicks { get; set; }

    public string Key { get; } = Guid.NewGuid().ToString("N");
}

/// <summary>A control on a page, its element and its page's readiness given by the test.</summary>
public sealed class FakeHost : IControlHost<FakeElement>
{
    public FakeHost() => Calls = new ControlCalls<FakeElement>(this);

    public ControlCalls<FakeElement> Calls { get; }

    public RecordingLogger Logger { get; } = new();

    /// <summary>What a lookup finds; a shown, enabled element unless the test says otherwise.</summary>
    public Func<FakeElement?> Next { get; set; } = () => new FakeElement();

    /// <summary>The page's readiness; ready unless the test says otherwise.</summary>
    public Func<ScopeReadiness> Scope { get; set; } = () => ScopeReadiness.Ready("Page");

    public Locator Locator { get; } = Locator.ByAutomationId("Target");

    public ControlCall NewCall() => new(Logger, "test", NearMissSettings.Default, "Page", "Target");

    public int DefaultTimeoutMs => 1000;

    public int PollingIntervalMs => 1;

    public int AnimationMs => 0;

    public ScopeReadiness ProbeScope() => Scope();

    public FakeElement? Locate(AttemptContext attempt) => Next();

    public FakeElement? TryFind() => Next();

    public bool? IsVisible(FakeElement element) => element.Visible;

    public void ScrollIntoView(FakeElement element, int timeoutMs)
    {
    }

    public string ScrollKey(FakeElement element) => element.Key;

    public string? InstanceKey(FakeElement element) => element.Key;

    public bool ChecksVisibilityBeforeAction(FakeElement element) => true;

    public void EnsureReadyForAction(FakeElement element)
    {
        if (!element.Enabled())
        {
            throw new ElementNotReadyException(Locator, NotReadyReason.Disabled);
        }
    }

    public ElementNotFoundException NotFound() => new(Locator);
}

/// <summary>A logger that keeps what was written.</summary>
public sealed class RecordingLogger : ITestLogger
{
    public List<LogEvent> Events { get; } = [];

    public void Write(LogEvent entry) => Events.Add(entry);

    public void Flush()
    {
    }

    public void Dispose()
    {
    }
}
