using Bruinet.Maui.Testing;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.CommunityToolkit;

/// <summary>
/// UI tests for the CommunityToolkit MediaElement, playing a six-second local tone.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "MediaElement")]
public class MediaElementTests
{
    private readonly MauiFixture _fixture;

    public MediaElementTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.CommunityToolkitMedia);
    }

    private CommunityToolkitMediaTestPage GetPage() => new(_fixture.Context);

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task MediaElement_ResolvesAndIsVisible()
    {
        GetPage().TestMediaElement.AssertExists()
            .AssertVisible();
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "TransportButtons")]
    public Task MediaElement_TransportButtons_AreControlObjects()
    {
        var media = GetPage().TestMediaElement;

        media.PlayPauseButton.AssertExists()
            .VolumeMuteButton.AssertExists()
            .RepeatButton.AssertExists()
            .RewindButton.AssertExists()
            .FastForwardButton.AssertExists();
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsPlaying")]
    public Task MediaElement_DoesNotAutoPlay()
    {
        var page = GetPage();

        page.TestMediaElement.AssertPlaying(false);
        Assert.False(page.TestMediaElement.IsPlaying());
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Play")]
    public Task MediaElement_Play_StartsPlaybackAndAdvances()
    {
        var page = GetPage();

        page.TestMediaElement.Play()
            .AssertPlaying(true)
            .AssertProgressGreaterThan(0)
            .Stop();
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Pause")]
    public Task MediaElement_PlayThenPause_Pauses()
    {
        GetPage().TestMediaElement.Play()
            .Pause()
            .AssertPlaying(false)
            .Stop();
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetPlaying")]
    public Task MediaElement_PauseWhenNotPlaying_IsNoOp()
    {
        GetPage().TestMediaElement.Pause()
            .SetPlaying(null)
            .AssertPlaying(false);
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetPlaying")]
    public Task MediaElement_SetPlaying_PlaysAndPauses()
    {
        GetPage().TestMediaElement.SetPlaying(true)
            .AssertPlaying(true)
            .SetPlaying(false)
            .AssertPlaying(false)
            .Stop();
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Stop")]
    public Task MediaElement_Stop_PausesAndReturnsToTheStart()
    {
        GetPage().TestMediaElement.Play()
            .AssertProgressGreaterThan(0)
            .Stop()
            .AssertPlaying(false)
            .AssertProgress(0)
            .AssertElapsed(0);
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "GetProgress")]
    public Task MediaElement_Progress_StartsAtZero()
    {
        GetPage().TestMediaElement.AssertProgress(0);
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "GetDuration")]
    public Task MediaElement_Duration_IsClipLength()
    {
        var page = GetPage();

        Assert.True(page.TestMediaElement.WaitOpened());

        var duration = page.TestMediaElement.GetDuration();
        Assert.NotNull(duration);
        Assert.InRange(duration.Value, 5, 7);
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "GetRemaining")]
    public Task MediaElement_BeforePlay_AllOfTheClipRemains()
    {
        GetPage().TestMediaElement.AssertElapsed(0)
            .AssertRemainingAtLeast(5)
            .AssertRemainingAtMost(7);
        return Task.CompletedTask;
    }

    [DeviceFact("it drives the player's transport controls, which the platform draws", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "PlayPauseButton")]
    public Task MediaElement_PlayPauseButton_ReportsTheSameStateAsTheComponent()
    {
        var media = GetPage().TestMediaElement;

        media.Play()
            .PlayPauseButton.AssertPlaying(true)
            .Stop()
            .PlayPauseButton.AssertPlaying(false);
        return Task.CompletedTask;
    }
}
