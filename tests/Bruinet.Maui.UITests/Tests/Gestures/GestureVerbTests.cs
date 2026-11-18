using Bruinet.Maui.Interfaces;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Gestures;

/// <summary>
/// Step 18: every gesture verb, and every way one can fail to bind.
/// </summary>
/// <remarks>
/// <para>
/// <b>These stand in for the unit tests the binding table cannot have.</b> <c>VerbBindings.Resolve</c>
/// is a pure function of an element and a declaration and ought to be testable in milliseconds
/// with no app - but it takes MAUI types, and MAUI 10 ships no plain <c>net10.0</c> assembly for
/// them, so there is nowhere in this repo such a test could live. Each row of the gestures page
/// was built to exercise one branch of the table instead, and <c>GetCapabilities</c> reports what
/// actually bound.
/// </para>
/// <para>
/// <b>Half of these assert a refusal, and that is the half that is new.</b> Before bindings, a
/// declaration was published verbatim: an element could advertise a verb its recognizers could
/// not serve and refuse it on use, and the refusal was <c>UIA_E_NOTSUPPORTED</c> - the same
/// answer as a typo, an unimplemented verb, or an element that was never declared. Now the
/// mismatch is caught when the app publishes the element, so the verb is not advertised at all.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "GestureBridge")]
public class GestureVerbTests
{
    private readonly MauiFixture _fixture;
    private readonly GesturesTestPage _page;

    public GestureVerbTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _page = _fixture.OpenGestures();
        _page.WaitLoaded(true, TestConstants.DefaultTestTimeoutMs);
    }

    private IMauiDriver Driver => _fixture.Context.Driver;

    /// <summary>
    /// A tap reaches the one-tap recognizer's command.
    /// </summary>
    /// <remarks>
    /// The count in the status is what makes this more than "something happened": a bridge that
    /// raised the command twice, or that raised a different row's, would still change the label.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Tap_RaisesTheOneTapRecognizer()
    {
        Driver.PerformGesture(GesturesTestPage.Targets.Tap, MauiGesture.Tap);

        Assert.Equal("Tapped x1", _page.TapStatus.GetText());
        return Task.CompletedTask;
    }

    /// <summary>
    /// A double tap reaches the two-tap recognizer, which is a different recognizer of the same
    /// type.
    /// </summary>
    /// <remarks>
    /// The reason the table keys on <c>NumberOfTapsRequired</c> rather than on the recognizer's
    /// type. Two rows on this page carry a <c>TapGestureRecognizer</c> and they mean different
    /// gestures.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task DoubleTap_RaisesTheTwoTapRecognizer()
    {
        Driver.PerformGesture(GesturesTestPage.Targets.DoubleTap, MauiGesture.DoubleTap);

        Assert.Equal("Double-tapped x1", _page.DoubleTapStatus.GetText());
        return Task.CompletedTask;
    }

    /// <summary>
    /// One recognizer declaring two directions answers both of them.
    /// </summary>
    /// <remarks>
    /// <c>SwipeDirection</c> is a flags enum and one recognizer commonly covers several, so the
    /// table matches on the flag. Matching on equality would pass for whichever direction was
    /// listed first and fail for the rest, which is a failure mode that hides well.
    /// </remarks>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Swipe_MatchesOnTheDirectionFlag()
    {
        Driver.PerformGesture(GesturesTestPage.Targets.Swipe, MauiGesture.SwipeLeft);
        Assert.Equal("Swiped Left or Right", _page.SwipeStatus.GetText());

        Driver.PerformGesture(GesturesTestPage.Targets.Swipe, MauiGesture.SwipeRight);
        Assert.Equal("Swiped Left or Right", _page.SwipeStatus.GetText());

        return Task.CompletedTask;
    }

    /// <summary>
    /// A verb MAUI cannot express reaches the app's own sink.
    /// </summary>
    /// <remarks>
    /// MAUI has no long-press recognizer. The nearest thing,
    /// <c>PointerGestureRecognizer.PointerPressedCommand</c>, means something else - pressing is
    /// not holding - so the table binds this to nothing and the app answers it itself. This test
    /// is what says the sink route works at all.
    /// </remarks>
    [WindowsOnlyFact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task LongPress_ReachesTheSinkThatNamedIt()
    {
        Driver.PerformGesture(GesturesTestPage.Targets.LongPress, MauiGesture.LongPress);

        Assert.Equal("Long-pressed", _page.LongPressStatus.GetText());
        return Task.CompletedTask;
    }

    [WindowsOnlyFact(
        Timeout = TestConstants.DefaultTestTimeoutMs,
        Device = "MauiHeadless removes the platform behavior before the gesture can be delivered")]
    public Task Tap_ReachesThePlatformBehaviorCommand()
    {
        Driver.PerformGesture(GesturesTestPage.Targets.TouchBehavior, MauiGesture.Tap);

        Assert.Equal("Tapped x1", _page.TapStatus.GetText());
        return Task.CompletedTask;
    }

    [WindowsOnlyFact(
        Timeout = TestConstants.DefaultTestTimeoutMs,
        Device = "MauiHeadless removes the platform behavior before the gesture can be delivered")]
    public Task LongPress_ReachesThePlatformBehaviorCommand()
    {
        Driver.PerformGesture(GesturesTestPage.Targets.TouchBehavior, MauiGesture.LongPress);

        Assert.Equal("Long-pressed", _page.LongPressStatus.GetText());
        return Task.CompletedTask;
    }

    /// <summary>
    /// A sink-owned verb carries its arguments through unchanged.
    /// </summary>
    /// <remarks>
    /// A pan with no distance is not a pan. The arguments are the only part of this verb that
    /// carries meaning, so a route that delivered the verb and dropped them would pass any test
    /// that only asked whether something happened.
    /// </remarks>
    [WindowsOnlyFact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Pan_CarriesItsArgumentsToTheSink()
    {
        Driver.PerformGesture(GesturesTestPage.Targets.Sink, MauiGesture.Pan, 30, -12);

        Assert.Equal("Panned 30,-12", _page.SinkStatus.GetText());
        return Task.CompletedTask;
    }

    /// <summary>
    /// A row that declares nothing is not on the bridge, however drivable it looks.
    /// </summary>
    /// <remarks>
    /// It carries a working one-tap recognizer, so an inferring bridge would publish it. The
    /// claim being tested is <c>GestureAutomation</c>'s own: declared, never inferred.
    /// </remarks>
    [WindowsOnlyFact(Timeout = TestConstants.DefaultTestTimeoutMs, Device = "it checks what the Windows UI Automation bridge publishes")]
    public Task UndeclaredRow_IsNotOnTheBridgeAtAll()
    {
        Assert.False(
            Driver.SupportsGesture(GesturesTestPage.Targets.NoDeclaration, MauiGesture.Tap),
            "A row that declares no verbs was published anyway, so the bridge is inferring.");

        Assert.Equal(GesturesTestPage.Untouched, _page.NoDeclarationStatus.GetText());
        return Task.CompletedTask;
    }

    /// <summary>
    /// A row that declares a verb it cannot perform does not advertise it.
    /// </summary>
    /// <remarks>
    /// <b>The behaviour bindings were built for.</b> This row says <c>DoubleTap</c> and carries a
    /// one-tap recognizer. Before, the declaration was published verbatim and the mismatch
    /// surfaced as a refusal when a test finally sent the verb; now nothing binds, the verb is
    /// not in the element's capabilities, and the app named the mismatch on its debug output when
    /// it published the element.
    /// </remarks>
    [WindowsOnlyFact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task MisdeclaredRow_DoesNotAdvertiseTheVerbItCannotDo()
    {
        Assert.False(
            Driver.SupportsGesture(GesturesTestPage.Targets.Unbindable, MauiGesture.DoubleTap),
            "The row declares DoubleTap and has only a one-tap recognizer, so nothing can perform "
            + "it - but the bridge advertised it anyway. Capabilities are being taken from the "
            + "declaration rather than from what bound.");

        Assert.Equal(GesturesTestPage.Untouched, _page.UnbindableStatus.GetText());
        return Task.CompletedTask;
    }

    /// <summary>
    /// A verb an element did not declare is refused, even when a sibling row answers it.
    /// </summary>
    /// <remarks>
    /// The double-tap row has a recognizer that a <c>Tap</c> could plausibly be routed to if
    /// anything were still searching. Nothing is: the row declared <c>DoubleTap</c> and that is
    /// the only verb bound to it.
    /// </remarks>
    [WindowsOnlyFact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task UndeclaredVerb_OnADeclaredRow_IsRefused()
    {
        Assert.True(
            Driver.SupportsGesture(GesturesTestPage.Targets.DoubleTap, MauiGesture.DoubleTap),
            "The double-tap row does not answer DoubleTap, so this test is not measuring what it "
            + "thinks it is.");

        Assert.False(
            Driver.SupportsGesture(GesturesTestPage.Targets.DoubleTap, MauiGesture.Tap),
            "The double-tap row answered Tap, which it never declared.");

        return Task.CompletedTask;
    }
}
