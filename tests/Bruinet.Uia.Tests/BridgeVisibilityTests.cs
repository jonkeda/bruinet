using Bruinet.Uia.TestHost;
using FlaUI.Core.AutomationElements;
using Xunit.Abstractions;

namespace Bruinet.Uia.Tests;

/// <summary>
/// Steps 4 and 6: can a custom provider on a child window be seen at all, and can it be seen
/// only where we want it to be?
/// </summary>
/// <remarks>
/// <para>
/// The design rests on one unproven claim: that a native provider hung off a child HWND of the
/// app's window joins the app's automation tree without disturbing it. The repo already records
/// the failure mode this replaces - overriding a WinUI control's automation peer collapsed the
/// entire tree for the app under test - so the precedent says prove it rather than assume it.
/// </para>
/// <para>
/// Step 6 is the second half: the bridge must be reachable by a test and invisible to Narrator,
/// Voice Access and Magnifier, all of which walk the control and content views. That is what
/// makes this instrumentation rather than a pollutant in the accessibility tree.
/// </para>
/// </remarks>
[Collection("BridgeHost")]
[Trait("Category", "Uia")]
public class BridgeVisibilityTests
{
    private readonly BridgeHostFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BridgeVisibilityTests(BridgeHostFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>Step 4. The one that can kill the design.</summary>
    [Fact]
    public void BridgeWindow_IsInTheAutomationTree()
    {
        var raw = _fixture.Automation.TreeWalkerFactory.GetRawViewWalker();

        var bridge = BridgeClient.FindByClassName(
            raw, _fixture.HostWindow, BruinetUiaIds.BridgeClassName);

        Assert.True(
            bridge is not null,
            "The bridge window is not in the automation tree under the host window. The "
            + "sidecar-provider design depends on this; without it the fallback is D-6, which "
            + "encodes verbs into ValuePattern and puts machine commands into the accessible "
            + "tree.");
    }

    /// <summary>Step 4. The provider, not merely the window, must be reachable.</summary>
    [Fact]
    public void TargetElements_AreInTheAutomationTree()
    {
        var raw = _fixture.Automation.TreeWalkerFactory.GetRawViewWalker();

        var target = BridgeClient.FindByClassName(
            raw, _fixture.HostWindow, BruinetUiaIds.TargetClassName);

        Assert.True(
            target is not null,
            "The bridge window was found but its child elements were not. The fragment root's "
            + "Navigate or its host provider is wrong.");

        Assert.Equal(
            BruinetUiaIds.TargetAutomationIdFor(HostTargets.Primary),
            target!.Properties.AutomationId.Value);
    }

    /// <summary>
    /// Step 4's regression guard: the app's own tree must be exactly as it was.
    /// </summary>
    /// <remarks>
    /// The measured failure this design exists to avoid did not look like a failure. The app
    /// rendered normally and every element simply stopped being addressable. So the assertion
    /// is not "the bridge works" but "the host window still reports what it reported", which is
    /// what would have caught it.
    /// </remarks>
    [Fact]
    public void Bridge_DoesNotDisturbTheHostWindow()
    {
        Assert.Equal(_fixture.WindowTitle, _fixture.HostWindow.Properties.Name.Value);
        Assert.True(
            _fixture.HostWindow.Properties.BoundingRectangle.Value.Width > 0,
            "The host window reports no bounds, which is what a collapsed tree looks like.");
    }

    /// <summary>
    /// Step 6. Raw yes, control no, content no.
    /// </summary>
    /// <remarks>
    /// Reports the full matrix rather than asserting only the interesting cell, because a
    /// negative here changes the design (D-5 falls back to a visible custom control with a
    /// recorded accessibility cost) and the next person needs the whole reading, not the verdict.
    /// </remarks>
    [Fact]
    public void Bridge_IsInTheRawViewOnly()
    {
        var factory = _fixture.Automation.TreeWalkerFactory;

        var inRaw = BridgeClient.FindByClassName(
            factory.GetRawViewWalker(), _fixture.HostWindow, BruinetUiaIds.TargetClassName);
        var inControl = BridgeClient.FindByClassName(
            factory.GetControlViewWalker(), _fixture.HostWindow, BruinetUiaIds.TargetClassName);
        var inContent = BridgeClient.FindByClassName(
            factory.GetContentViewWalker(), _fixture.HostWindow, BruinetUiaIds.TargetClassName);

        _output.WriteLine("| View | Bridge target found |");
        _output.WriteLine("|---|---|");
        _output.WriteLine($"| Raw | {(inRaw is not null ? "yes" : "NO")} |");
        _output.WriteLine($"| Control | {(inControl is not null ? "yes" : "NO")} |");
        _output.WriteLine($"| Content | {(inContent is not null ? "yes" : "NO")} |");

        Assert.True(inRaw is not null, "A test client must be able to reach the bridge.");

        Assert.True(
            inControl is null,
            "The bridge appears in the control view, where Narrator and Voice Access will read "
            + "it. IsControlElement is not being honoured on the target provider.");

        Assert.True(
            inContent is null,
            "The bridge appears in the content view. IsContentElement is not being honoured.");
    }

    /// <summary>
    /// Step 6. Whether the ordinary search finds a raw-view-only element.
    /// </summary>
    /// <remarks>
    /// This decides what the client extension in step 12 has to do. If <c>FindFirstDescendant</c>
    /// applies the control view by default, every bridge lookup needs an explicit raw walk and
    /// the extension has to supply one; if it walks raw, callers can use the ordinary search.
    /// Reported either way, and asserted only on the part that must hold.
    /// </remarks>
    [ProbeFact("reports whether an ordinary search reaches the bridge")]
    public void OrdinarySearch_ReachesTheBridgeOrDoesNot()
    {
        AutomationElement? found;
        try
        {
            found = _fixture.HostWindow.FindFirstDescendant(
                cf => cf.ByClassName(BruinetUiaIds.TargetClassName));
        }
        catch (Exception ex)
        {
            found = null;
            _output.WriteLine($"FindFirstDescendant threw: {ex.GetType().Name}: {ex.Message}");
        }

        _output.WriteLine(
            found is not null
                ? "FindFirstDescendant reaches raw-view-only elements: the client extension can "
                  + "use the ordinary search."
                : "FindFirstDescendant does NOT reach raw-view-only elements: the client "
                  + "extension must walk the raw view explicitly.");

        // Deliberately not asserted. Either answer is workable and the answer is the deliverable;
        // Bridge_IsInTheRawViewOnly is what must hold.
        Assert.True(true);
    }

    /// <summary>Both targets are published, and they are siblings of one another.</summary>
    /// <remarks>
    /// Sibling navigation is the part of the fragment contract a single child cannot exercise,
    /// and getting it wrong produces a tree that enumerates one element forever.
    /// </remarks>
    [Fact]
    public void TargetElements_AreSiblings()
    {
        var raw = _fixture.Automation.TreeWalkerFactory.GetRawViewWalker();

        var bridge = BridgeClient.FindByClassName(
            raw, _fixture.HostWindow, BruinetUiaIds.BridgeClassName);
        Assert.NotNull(bridge);

        var found = new List<string>();
        var child = raw.GetFirstChild(bridge!);

        while (child is not null)
        {
            found.Add(child.Properties.AutomationId.ValueOrDefault ?? string.Empty);
            child = raw.GetNextSibling(child);
        }

        Assert.Equal(
            [
                BruinetUiaIds.TargetAutomationIdFor(HostTargets.Primary),
                BruinetUiaIds.TargetAutomationIdFor(HostTargets.Secondary),
            ],
            found);
    }
}
