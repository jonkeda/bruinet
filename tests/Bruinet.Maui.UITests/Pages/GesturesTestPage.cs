namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// The gestures page: one row per way a gesture can reach a MAUI element.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every row is a pair - a target and its status.</b> The target is what a verb is aimed at;
/// the status is the only thing that can tell a verb which arrived from a verb which returned
/// success and did nothing. That distinction is the reason this page exists rather than the
/// gestures being tested on the container page, where the outcome of a swipe is a control
/// Windows automation cannot see at all.
/// </para>
/// <para>
/// The last two rows have no verbs to drive. <c>NoDeclarationTarget</c> declares nothing and so
/// is not on the bridge; <c>UnbindableTarget</c> declares a verb its recognizer cannot serve.
/// Both are exposed here because a test has to be able to ask about them.
/// </para>
/// </remarks>
public class GesturesTestPage : PageObjectBase<GesturesTestPage>
{
    public GesturesTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "GesturesTestPage";

    /// <summary>What every status label says before anything reaches its row.</summary>
    /// <remarks>
    /// Mirrors <c>GesturesViewModel.Untouched</c>. Duplicated for the same reason the page enum
    /// is: a UI test must not reference the app it drives.
    /// </remarks>
    public const string Untouched = "Untouched";

    /// <summary>The single-tap row.</summary>
    public Label<GesturesTestPage> TapStatus => new(this, "GestureTapStatus");

    /// <summary>The two-tap row.</summary>
    public Label<GesturesTestPage> DoubleTapStatus => new(this, "GestureDoubleTapStatus");

    /// <summary>The swipe row, whose recognizer declares two directions at once.</summary>
    public Label<GesturesTestPage> SwipeStatus => new(this, "GestureSwipeStatus");

    /// <summary>The long-press row, which only the app's sink can answer.</summary>
    public Label<GesturesTestPage> LongPressStatus => new(this, "GestureLongPressStatus");

    /// <summary>The pan row, the other verb with no MAUI API behind it.</summary>
    public Label<GesturesTestPage> SinkStatus => new(this, "GestureSinkStatus");

    /// <summary>The row that declares nothing. Expected to stay untouched.</summary>
    public Label<GesturesTestPage> NoDeclarationStatus => new(this, "GestureNoDeclarationStatus");

    /// <summary>The row that declares a verb it cannot serve. Expected to stay untouched.</summary>
    public Label<GesturesTestPage> UnbindableStatus => new(this, "GestureUnbindableStatus");

    /// <summary>The AutomationId of a row's target, which is what a verb is addressed to.</summary>
    public static class Targets
    {
        /// <summary>Answers <c>Tap</c> through a one-tap recognizer.</summary>
        public const string Tap = "GestureTapTarget";

        /// <summary>Answers <c>DoubleTap</c> through a two-tap recognizer.</summary>
        public const string DoubleTap = "GestureDoubleTapTarget";

        /// <summary>Answers <c>SwipeLeft</c> and <c>SwipeRight</c> through one recognizer.</summary>
        public const string Swipe = "GestureSwipeTarget";

        /// <summary>Answers <c>LongPress</c> through the app's sink.</summary>
        public const string LongPress = "GestureLongPressTarget";

        /// <summary>Answers Tap and LongPress through the attached platform behavior.</summary>
        public const string TouchBehavior = "GestureTouchBehaviorTarget";

        /// <summary>Answers <c>Pan</c> through the app's sink.</summary>
        public const string Sink = "GestureSinkTarget";

        /// <summary>Declares nothing, so it is not on the bridge.</summary>
        public const string NoDeclaration = "GestureNoDeclarationTarget";

        /// <summary>Declares <c>DoubleTap</c> with a one-tap recognizer, so nothing binds.</summary>
        public const string Unbindable = "GestureUnbindableTarget";
    }
}
