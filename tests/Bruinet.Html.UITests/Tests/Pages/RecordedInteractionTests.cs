using Bruinet.Html.Testing;
using Bruinet.Core.Recording;
using Bruinet.Core.Testing;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Pages;

/// <summary>
/// What a recorded video's touch marks are drawn from, in a browser: a click reports its target's
/// bounds in the viewport, taken before it runs (AD-024).
/// </summary>
/// <remarks>The log is the process's: this class runs alone, so no other test's click is in it.</remarks>
[Collection(Alone)]
public sealed class RecordedInteractionTests : BlazorSampleTestBase
{
    private const string Alone = "Recorded interactions";

    [BrowserFact("a recorded click carries its bounds in the viewport, which needs layout")]
    public void AClick_IsLogged_WithItsButtonsBounds_InTheViewport()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);
        var log = new Log();

        var target = ((IRecordable)Context).RecordingTarget ?? throw new InvalidOperationException("The page records nothing.");
        using (Interactions.Begin(target, log))
        {
            page.IncrementButton.Click();
        }

        page.CountDisplay.AssertText("Current count: 1");

        var tap = Assert.Single(log.Taken);
        Assert.Equal(InteractionKind.Tap, tap.Kind);
        Assert.Contains("increment-btn", tap.Label, StringComparison.Ordinal);
        Assert.False(tap.Bounds.IsEmpty);
        Assert.True(tap.Bounds.X >= 0 && tap.Bounds.Y >= 0, $"The button at {tap.Bounds} is in the viewport.");
    }

    private sealed class Log : IInteractionLog
    {
        public List<Interaction> Taken { get; } = [];

        public void Add(Interaction interaction) => Taken.Add(interaction);
    }
}

[CollectionDefinition("Recorded interactions", DisableParallelization = true)]
public sealed class RecordedInteractionsCollection;
