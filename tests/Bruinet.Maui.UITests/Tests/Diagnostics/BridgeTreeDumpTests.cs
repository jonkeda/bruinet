using Bruinet.Maui.Testing;
using Bruinet.Maui.FlaUI;
using Xunit.Abstractions;

namespace Bruinet.Maui.UITests.Tests.Diagnostics;

/// <summary>
/// Prints the raw automation tree just below the app window, so a bridge that is not working
/// can be told apart from a bridge that is not there.
/// </summary>
/// <remarks>
/// <para>
/// A measurement, not a regression test. The failures it exists to separate all reach a test as
/// the same sentence - "the element was not found" - and differ entirely in what to do:
/// </para>
/// <list type="bullet">
/// <item>no bridge window at all: the app was not built with the automation sources;</item>
/// <item>a bridge window with no fragment root: <c>WM_GETOBJECT</c> is not being answered,
/// which is a fault in the provider;</item>
/// <item>a fragment root with no children: nothing declared verbs, or registration failed.</item>
/// </list>
/// <para>
/// Names a FlaUI type, so it lives here with the other Windows-only diagnostics - this whole
/// folder is excluded from the Appium head.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "Diagnostics")]
public class BridgeTreeDumpTests
{
    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BridgeTreeDumpTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.NavigateToContainerModule();
    }

    /// <summary>Reports the tree; asserts only that the app still has one.</summary>
    [DeviceFact("it drives the Windows driver (FlaUI) itself", Timeout = TestConstants.LongTestTimeoutMs)]
    public Task DumpRawTreeBelowTheWindow()
    {
        var driver = (FlaUIMauiDriver)_fixture.Context.Driver;

        var tree = driver.DescribeGestureBridge();
        _output.WriteLine(tree);
        _output.WriteLine($"HasGestureBridge: {driver.HasGestureBridge()}");

        Assert.False(
            string.IsNullOrWhiteSpace(tree),
            "The app window reported no tree at all, which is what a collapsed automation tree "
            + "looks like.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reports what a <c>SwipeView</c> exposes once the bridge has opened it.
    /// </summary>
    /// <remarks>
    /// The open swipe view is the one place the bridge can act without the outcome being
    /// visible: the <c>SwipeView</c> publishes no <c>AutomationId</c> on Windows, so the only
    /// way to see whether the swipe took is to look at what appeared. This prints it.
    /// </remarks>
    [DeviceFact("it drives the Windows driver (FlaUI) itself", Timeout = TestConstants.LongTestTimeoutMs)]
    public Task DumpSwipeViewAfterOpening()
    {
        var driver = (FlaUIMauiDriver)_fixture.Context.Driver;

        var before = SwipeRelatedLines(driver);
        _output.WriteLine("--- before ---");
        _output.WriteLine(before);

        driver.PerformGesture("TestSwipeView", Bruinet.Maui.Interfaces.MauiGesture.SwipeRight);
        Bruinet.Core.Calls.Settle.Until(() => SwipeRelatedLines(driver) != before, 500);

        _output.WriteLine("--- after SwipeRight ---");
        _output.WriteLine(SwipeRelatedLines(driver));

        return Task.CompletedTask;
    }

    /// <summary>Every line of the app's automation tree that mentions a swipe.</summary>
    private static string SwipeRelatedLines(FlaUIMauiDriver driver)
    {
        var lines = driver.GetAutomationTree()
            .Split('\n')
            .Where(l => l.Contains("Swipe", StringComparison.OrdinalIgnoreCase)
                        || l.Contains("Delete", StringComparison.OrdinalIgnoreCase));

        return string.Join(Environment.NewLine, lines);
    }
}
