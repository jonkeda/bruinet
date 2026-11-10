using Bruinet.Maui.Testing;
using Bruinet.Maui.FlaUI;
using Bruinet.Maui.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace Bruinet.Maui.UITests.Tests.Diagnostics;

/// <summary>
/// Step 46: whether the app under test took the foreground, counted rather than watched for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Step 15's done-when was a person typing in Visual Studio throughout a run.</b> That check
/// was never performed and cannot be performed in CI, and when somebody finally did watch, the
/// app was reported popping over the editor. The watchdog in <c>FlaUIMauiDriver</c> now pushes
/// the app back whenever any of its windows becomes the foreground, and counts each time.
/// </para>
/// <para>
/// <b>The count is not asserted to be zero, and that is deliberate for now.</b> Launch is one
/// grab the app is certain to make - Windows gives a new process the foreground - and the
/// watchdog's job there is to make it brief rather than impossible. What this pins is that
/// navigating the app, which is what the rest of the suite does, causes no further grabs.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "Diagnostics")]
[Trait("Stage", "Background")]
public class ForegroundWatchdogTests
{
    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ForegroundWatchdogTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>Walking the app does not raise it over the person's work.</summary>
    [DeviceFact("it drives the Windows driver (FlaUI) itself", Timeout = TestConstants.LongTestTimeoutMs)]
    public Task Navigating_DoesNotTakeTheForeground()
    {
        var driver = (FlaUIMauiDriver)_fixture.Context.Driver;
        var before = driver.ForegroundGrabs;

        foreach (var page in new[] { SamplePage.Text, SamplePage.Dialogs, SamplePage.Scroll, SamplePage.Gestures })
        {
            _fixture.Open(page);
        }

        var during = driver.ForegroundGrabs - before;
        _output.WriteLine($"Grabs since launch: {driver.ForegroundGrabs}; while navigating: {during}.");

        Assert.True(
            during == 0,
            $"The app took the foreground {during} time(s) while being navigated, and was pushed "
            + "back each time. Every one of those was a moment it sat over whatever the person at "
            + "the machine was doing.");

        return Task.CompletedTask;
    }
}
