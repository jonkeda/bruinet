using Bruinet.Maui.FlaUI;
using Bruinet.Maui.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace Bruinet.Maui.UITests.Tests.Diagnostics;

/// <summary>
/// A fact that runs only when a stress run is asked for.
/// </summary>
/// <remarks>
/// A minute or more each, so not part of an ordinary run - but kept, because what they reproduce
/// is otherwise invisible until it has already ended a full suite.
/// </remarks>
public sealed class StressFactAttribute : FactAttribute
{
    public StressFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("BRUINET_STRESS") != "1")
        {
            Skip = "A stress reproduction, a minute or more long. Set BRUINET_STRESS=1 to run it.";
        }
    }
}

/// <summary>
/// Reproduces UI Automation retiring the app window element under page churn, and pins the fix.
/// </summary>
/// <remarks>
/// <para>
/// <b>What looked like an app freeze.</b> Two of eight full runs ended with every later test
/// failing in milliseconds against an app that was, when finally captured, idle and responding.
/// UI Automation had retired the driver's element for the app's window, and the driver never asked
/// again. The trigger was the bridge calling <c>UiaDisconnectProvider</c> whenever a page withdrew
/// its targets: this walk, with a second thread calling the bridge throughout, saw the window
/// element retired seven or eight times in 150 page changes with it, and never without it.
/// </para>
/// <para>
/// <b>Asserts both halves of the fix.</b> The walk completes - the driver re-attaches a retired
/// root rather than going blind - and no re-attachment was needed, which is what shows the
/// trigger is gone rather than merely survived.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "Stress")]
public class NavigationStressTests
{
    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public NavigationStressTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [StressFact(Timeout = 10 * 60 * 1000)]
    public Task NavigatingWhileTheBridgeIsBusy_NeverRetiresTheWindowElement()
    {
        var driver = (FlaUIMauiDriver)_fixture.Context.Driver;
        var before = driver.RootReattachments;

        using var stop = new CancellationTokenSource();
        var calls = 0;

        // Client traffic during page transitions made the retirement several times likelier -
        // once in 150 page changes without it, seven or eight with.
        var busy = new Thread(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    driver.CurrentRoute();
                    driver.NavigationDepth();
                    Interlocked.Increment(ref calls);
                }
                catch
                {
                    // Transient answers during a transition are expected; the walk is what is
                    // being tested.
                }
            }
        })
        { IsBackground = true };

        busy.Start();

        SamplePage[] pages =
        [
            SamplePage.GridCollection, SamplePage.Container, SamplePage.Collection,
            SamplePage.Container, SamplePage.Gestures,
        ];

        for (var i = 0; i < 150; i++)
        {
            _fixture.Open(pages[i % pages.Length]);
        }

        stop.Cancel();

        var reattached = driver.RootReattachments - before;
        _output.WriteLine($"150 page changes, {calls} concurrent bridge calls, {reattached} root re-attachments.");

        Assert.True(
            reattached == 0,
            $"UI Automation retired the window element {reattached} time(s). The driver recovered, "
            + "but something is invalidating it again - see rca-app-freeze-was-a-stale-root.md, "
            + "and check nothing has put UiaDisconnectProvider back on the withdrawal path.");

        return Task.CompletedTask;
    }
}
