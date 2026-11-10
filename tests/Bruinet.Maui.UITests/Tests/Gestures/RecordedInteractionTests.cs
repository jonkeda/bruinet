using Bruinet.Maui.Testing;
using System.Drawing;
using Bruinet.Core.Recording;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Gestures;

/// <summary>
/// What a recorded video's touch marks are drawn from: while a scene records, each action reports
/// itself with its target's bounds, taken before it runs (AD-024).
/// </summary>
/// <remarks>
/// A framework test of the driver: the log is installed here as a scene recording installs it,
/// without recording a video.
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Stage", "Background")]
[Trait("Pattern", "GestureBridge")]
public class RecordedInteractionTests
{
    private readonly MauiFixture _fixture;

    public RecordedInteractionTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.NavigateToContainerModule();
    }

    private ContainerTestPage Page => _fixture.ContainerTestPage;

    [DeviceFact("it logs input in physical pixels", Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task ATap_AndAPullToRefresh_AreLogged_OverTheirControls_InPhysicalPixels()
    {
        var log = new Log();

        var target = ((IRecordable)_fixture.Context).RecordingTarget
            ?? new RecordingTarget.Window(0); // a head that cannot say what it shows: its reports name no target either
        using (Interactions.Begin(target, log))
        {
            Page.TriggerRefreshButton.Click();
            Page.RefreshText.WaitText("refreshed 1", TestConstants.DefaultTestTimeoutMs);

            // Pull to refresh: a control the tree cannot show, so its bounds come from the bridge.
            _fixture.Context.Driver.PerformGesture("TestRefreshView", MauiGesture.SwipeDown);
            Page.RefreshText.WaitText("refreshed 2", TestConstants.DefaultTestTimeoutMs);
        }

        Assert.Equal([InteractionKind.Tap, InteractionKind.Swipe], log.Taken.Select(interaction => interaction.Kind));
        var tap = log.Taken[0];
        Assert.False(tap.Bounds.IsEmpty);
        Assert.Equal("TriggerRefreshButton", tap.Label);

        var swipe = log.Taken[1];
        Assert.False(swipe.Bounds.IsEmpty, "The RefreshView's bounds came from the bridge.");

        // Both in physical pixels, as the video's frames are: the button sits below the RefreshView.
        Assert.True(tap.Bounds.Top >= swipe.Bounds.Bottom - 1, $"The button at {tap.Bounds} is below the RefreshView at {swipe.Bounds}.");
        Assert.True(swipe.From!.Value.Y < swipe.To!.Value.Y, "A pull to refresh is drawn downwards.");
        return Task.CompletedTask;
    }

    private sealed class Log : IInteractionLog
    {
        public List<Interaction> Taken { get; } = [];

        public void Add(Interaction interaction) => Taken.Add(interaction);
    }
}
