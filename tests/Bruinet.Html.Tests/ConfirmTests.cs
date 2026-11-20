using System.Diagnostics;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Html.Controls;
using Bruinet.Html.Controls.Text;

namespace Bruinet.Html.Tests;

/// <summary>
/// An action is confirmed, never repeated (R0, plan step 6): its effect is read in a phase with a
/// budget of its own (R3).
/// </summary>
public class ConfirmTests
{
    private readonly Mock<IHtmlTestContext> _context = new();
    private readonly Mock<IHtmlElement> _input = new();

    public ConfirmTests()
    {
        _context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(1000), PollingInterval = TimeSpan.FromMilliseconds(1) });
        _context.Setup(c => c.Logger).Returns(Mock.Of<ITestLogger>());
        _context.Setup(c => c.Context).Returns(() => _context.Object);
        _context.Setup(c => c.IsIdle()).Returns(true);
        _context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Title"))).Returns(_input.Object);
        _input.Setup(e => e.Visible).Returns(true);
        _input.Setup(e => e.Enabled).Returns(true);
    }

    private sealed class Page(IHtmlTestContext context) : PageObjectBase<Page>(context)
    {
        public TextInputControl<Page> Title => new(this, Locator.ByAutomationId("Title"));

        public Picker Pick => new(this);
    }

    /// <summary>A control whose effect reads as a list, as a select's selections do.</summary>
    private sealed class Picker(IHtmlScope<Page> page) : ViewBase<Page>(page, Locator.ByAutomationId("Title"))
    {
        public Page Choose(string wanted) => RunDoWithElement(element => ActAndConfirm<IReadOnlyList<string>>(
            "Choose", element.Click, () => ["Project 001", "Other"], selected => selected!.Contains(wanted), $"'{wanted}' selected", 200));
    }

    [Fact]
    public void AnElementReplacedAfterTheAction_FailsAsStale_AndTheActionRanOnce()
    {
        _input.Setup(e => e.InputValue).Throws(new StaleElementException());

        var error = Record.Exception(() => new Page(_context.Object).Title.SetText("abc", timeoutMs: 300));

        var stale = Assert.IsType<StaleElementException>(error);
        Assert.Contains("ran once", stale.Message);
        _input.Verify(e => e.Fill("abc"), Times.Once);
    }

    [Fact]
    public void TheConfirmation_HasABudgetOfItsOwn_NotWhatTheResolveLeft()
    {
        // Disabled for most of the 300 ms budget, so the resolve phase leaves little. The field
        // shows the value only to a read given a real budget, as a Playwright read with a timeout.
        var clock = Stopwatch.StartNew();
        _input.Setup(e => e.Enabled).Returns(() => clock.ElapsedMilliseconds > 250);
        var filled = string.Empty;
        _input.Setup(e => e.Fill(It.IsAny<string>())).Callback<string>(value => filled = value);
        _input.Setup(e => e.InputValue).Returns(() => CallBudget.RemainingOr(0) > 150 ? filled : string.Empty);

        new Page(_context.Object).Title.SetText("abc", timeoutMs: 300);

        _input.Verify(e => e.Fill("abc"), Times.Once);
    }

    [Fact]
    public void AFieldThatKeepsLessThanWasTyped_FailsSetText_AndTakesOverfill()
    {
        // A field with a longest of four: it keeps the first four characters of what is filled.
        var kept = string.Empty;
        _input.Setup(e => e.Fill(It.IsAny<string>())).Callback<string>(value => kept = value[..Math.Min(4, value.Length)]);
        _input.Setup(e => e.InputValue).Returns(() => kept);

        var page = new Page(_context.Object);
        var error = Record.Exception(() => page.Title.SetText("abcdefgh", timeoutMs: 100));
        page.Title.Overfill("abcdefgh", timeoutMs: 300);

        Assert.Contains("did not take it", Assert.IsType<InvalidOperationException>(error).Message);
        Assert.Equal("abcd", page.Title.GetText());

        // A read-only field takes no text, as with SetText.
        _input.Setup(e => e.GetAttribute("readonly")).Returns(string.Empty);
        Assert.IsType<ActionRefusedException>(Record.Exception(() => page.Title.Overfill("abcdefgh", timeoutMs: 100)));
    }

    [Fact]
    public void AnEffectReadAsAList_IsNamedByItsItems()
    {
        var error = Record.Exception(() => new Page(_context.Object).Pick.Choose("00002 - Project 001"));

        Assert.Contains("it reads '[Project 001, Other]'", Assert.IsType<InvalidOperationException>(error).Message);
    }

    [Fact]
    public void ABusyElement_IsNotActedOn_UntilItIsDone()
    {
        var busy = true;
        var clock = Stopwatch.StartNew();
        _input.Setup(e => e.GetAttribute("aria-busy")).Returns(() => (busy = clock.ElapsedMilliseconds < 150) ? "true" : "false");
        var filled = string.Empty;
        _input.Setup(e => e.Fill(It.IsAny<string>())).Callback<string>(value => { Assert.False(busy); filled = value; });
        _input.Setup(e => e.InputValue).Returns(() => filled);

        new Page(_context.Object).Title.SetText("abc", timeoutMs: 1000);

        _input.Verify(e => e.Fill("abc"), Times.Once);
    }
}
