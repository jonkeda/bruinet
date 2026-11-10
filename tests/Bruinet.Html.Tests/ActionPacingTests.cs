using System.Diagnostics;
using Bruinet.Core;
using Bruinet.Core.Recording;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Text;

namespace Bruinet.Html.Tests;

[CollectionDefinition(nameof(ActionPacingTests), DisableParallelization = true)]
public sealed class ActionPacingCollection;

/// <summary>
/// While a video records, every action takes at least the pause, so the video shows it; an action
/// that already took longer waits nothing more. Reads never pause, an action that runs another
/// pauses once, and a run that does not record never waits (AD-024).
/// </summary>
[Collection(nameof(ActionPacingTests))]
public sealed class ActionPacingTests
{
    private const int PauseMs = 200;

    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<IHtmlElement> _button = new();
    private readonly Mock<IHtmlElement> _field = new();
    private string _value = string.Empty;

    public ActionPacingTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = 2000, PageLoad = 2000, PollingInterval = 1 });
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Go"))).Returns(_button.Object);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Name"))).Returns(_field.Object);
        foreach (var element in new[] { _button, _field })
        {
            element.Setup(e => e.Visible).Returns(true);
            element.Setup(e => e.Enabled).Returns(true);
        }

        _field.Setup(e => e.Text).Returns("Ann");
        _field.Setup(e => e.InputValue).Returns(() => _value);
        _field.Setup(e => e.SendKeys(It.IsAny<string>(), It.IsAny<TextInputMethod>())).Callback<string, TextInputMethod>((text, _) => _value += text);
        _field.Setup(e => e.Clear()).Callback(() => _value = string.Empty);
    }

    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public override bool IsLoaded() => true;

        public ButtonControl<Page> Go => new(this, Locator.ByAutomationId("Go"));

        public TextInputControl<Page> Field => new(this, Locator.ByAutomationId("Name"));
    }

    private static int Timed(Action action)
    {
        var start = Stopwatch.GetTimestamp();
        action();
        return (int)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    [Fact]
    public void Without_a_recording_an_action_never_waits()
    {
        var page = new Page(_context.Object);

        Assert.Null(ActionPacing.Pause);
        Assert.InRange(Timed(() => page.Go.Click().Go.Click().Go.Click()), 0, PauseMs);
    }

    [Fact]
    public void While_recording_a_quick_action_takes_at_least_the_pause()
    {
        var page = new Page(_context.Object);

        using (ActionPacing.Begin(TimeSpan.FromMilliseconds(PauseMs)))
        {
            Assert.InRange(Timed(() => page.Go.Click()), PauseMs - 5, PauseMs * 3);
            Assert.InRange(Timed(() => page.Go.Click().Field.TypeText("Bo")), (2 * PauseMs) - 5, PauseMs * 5);
        }

        Assert.Null(ActionPacing.Pause);
    }

    [Fact]
    public void An_action_that_took_longer_than_the_pause_waits_nothing_more()
    {
        _button.Setup(e => e.Click()).Callback(() => Thread.Sleep(PauseMs + 100));
        var page = new Page(_context.Object);

        using (ActionPacing.Begin(TimeSpan.FromMilliseconds(PauseMs)))
        {
            Assert.InRange(Timed(() => page.Go.Click()), PauseMs + 95, PauseMs + 100 + (PauseMs / 2));
        }
    }

    [Fact]
    public void A_read_never_pauses()
    {
        var page = new Page(_context.Object);

        using (ActionPacing.Begin(TimeSpan.FromMilliseconds(PauseMs)))
        {
            Assert.InRange(Timed(() => _ = page.Field.GetText()), 0, PauseMs - 50);
        }
    }

    [Fact]
    public void An_action_that_runs_another_pauses_once()
    {
        var page = new Page(_context.Object);
        _button.Setup(e => e.Click()).Callback(() => page.Field.Clear());

        using (ActionPacing.Begin(TimeSpan.FromMilliseconds(PauseMs)))
        {
            Assert.InRange(Timed(() => page.Go.Click()), PauseMs - 5, (2 * PauseMs) - 20);
        }
    }

    [Fact]
    public void A_failed_action_does_not_pause()
    {
        _button.Setup(e => e.Click()).Throws(new InvalidOperationException("Broken."));
        var page = new Page(_context.Object);

        using (ActionPacing.Begin(TimeSpan.FromMilliseconds(PauseMs)))
        {
            Assert.InRange(Timed(() => Assert.Throws<InvalidOperationException>(() => page.Go.Click())), 0, PauseMs - 50);
        }
    }
}
