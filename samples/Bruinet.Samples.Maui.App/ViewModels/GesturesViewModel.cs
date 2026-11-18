using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Samples.Maui.App.Automation;

namespace Bruinet.Samples.Maui.App.ViewModels;

/// <summary>
/// The gestures page: one row per way a gesture can reach a MAUI element.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every row records what reached it, not merely that something did.</b> A status of
/// "Tapped x1" and a status of "Tapped x2" are different facts, and a test that can only see
/// "something happened" cannot tell a `Tap` that arrived once from one that arrived twice - which
/// is exactly the bug a gesture bridge is most likely to have.
/// </para>
/// <para>
/// <b>Commands, not event handlers.</b> <c>SendTapped</c>, <c>SendSwiped</c> and their relatives
/// are internal in MAUI 10, so a recognizer's event cannot be raised from outside the framework
/// and only its <c>Command</c> can. That is a constraint on the app under test as much as on the
/// bridge, and it is the reason every row here binds a command.
/// </para>
/// </remarks>
public class GesturesViewModel : ParentViewModel
{
    private int _tapCount;
    private int _doubleTapCount;
    private string _tapStatus = Untouched;
    private string _doubleTapStatus = Untouched;
    private string _swipeStatus = Untouched;
    private string _longPressStatus = Untouched;
    private string _sinkStatus = Untouched;
    private string _noDeclarationStatus = Untouched;
    private string _unbindableStatus = Untouched;

    /// <summary>
    /// What every row says before anything has reached it.
    /// </summary>
    /// <remarks>
    /// One shared constant so a test asserting "nothing happened" and a row initialising itself
    /// cannot drift apart. The negative rows depend on this being exact.
    /// </remarks>
    public const string Untouched = "Untouched";

    public GesturesViewModel()
    {
        Sink = new GestureVerbSink(this);

        TapCommand = new RelayCommand(() => TapStatus = $"Tapped x{++_tapCount}");
        LongPressCommand = new RelayCommand(() => LongPressStatus = "Long-pressed");
        DoubleTapCommand = new RelayCommand(() => DoubleTapStatus = $"Double-tapped x{++_doubleTapCount}");
        SwipeCommand = new RelayCommand<string>(direction => SwipeStatus = $"Swiped {direction}");
        NoDeclarationCommand = new RelayCommand(() => NoDeclarationStatus = "Reached");
        UnbindableCommand = new RelayCommand(() => UnbindableStatus = "Reached");
    }

    /// <summary>
    /// The app's own answer to the verbs no MAUI API can deliver.
    /// </summary>
    /// <remarks>
    /// Exposed on the view model so markup can attach it with a binding. It takes the view model
    /// in its constructor, which is why it is built here rather than declared as a resource: the
    /// sink's whole job is to land a gesture's outcome where the gesture would have landed it.
    /// </remarks>
    public IBruinetGestureSink Sink { get; }

    /// <summary>Raised by a single-tap recognizer.</summary>
    public ICommand TapCommand { get; }

    /// <summary>Raised by a platform behavior's long press.</summary>
    public ICommand LongPressCommand { get; }

    /// <summary>Raised by a two-tap recognizer.</summary>
    public ICommand DoubleTapCommand { get; }

    /// <summary>Raised by a swipe recognizer; the parameter names the direction.</summary>
    public ICommand SwipeCommand { get; }

    /// <summary>
    /// Raised by the row that declares nothing to the bridge.
    /// </summary>
    /// <remarks>
    /// It has a working recognizer on purpose. The row proves that an element is absent from the
    /// bridge because it did not declare itself, not because it had nothing to offer - which is
    /// the whole claim behind "declared, never inferred".
    /// </remarks>
    public ICommand NoDeclarationCommand { get; }

    /// <summary>
    /// Raised by the row that declares a verb it cannot perform - so, never.
    /// </summary>
    /// <remarks>
    /// The row declares <c>DoubleTap</c> while carrying a single-tap recognizer. Nothing can
    /// raise this, and that is the point: the mismatch is reported when the app publishes the
    /// element, so this status staying at <see cref="Untouched"/> is the assertion.
    /// </remarks>
    public ICommand UnbindableCommand { get; }

    /// <summary>What reached the single-tap row.</summary>
    public string TapStatus
    {
        get => _tapStatus;
        private set => SetProperty(ref _tapStatus, value);
    }

    /// <summary>What reached the double-tap row.</summary>
    public string DoubleTapStatus
    {
        get => _doubleTapStatus;
        private set => SetProperty(ref _doubleTapStatus, value);
    }

    /// <summary>What reached the swipe row.</summary>
    public string SwipeStatus
    {
        get => _swipeStatus;
        private set => SetProperty(ref _swipeStatus, value);
    }

    /// <summary>What reached the long-press row, which only a sink can deliver.</summary>
    public string LongPressStatus
    {
        get => _longPressStatus;
        set => SetProperty(ref _longPressStatus, value);
    }

    /// <summary>What reached the sink-owned row.</summary>
    public string SinkStatus
    {
        get => _sinkStatus;
        set => SetProperty(ref _sinkStatus, value);
    }

    /// <summary>What reached the undeclared row. Expected to stay untouched.</summary>
    public string NoDeclarationStatus
    {
        get => _noDeclarationStatus;
        private set => SetProperty(ref _noDeclarationStatus, value);
    }

    /// <summary>What reached the misdeclared row. Expected to stay untouched.</summary>
    public string UnbindableStatus
    {
        get => _unbindableStatus;
        private set => SetProperty(ref _unbindableStatus, value);
    }
}
