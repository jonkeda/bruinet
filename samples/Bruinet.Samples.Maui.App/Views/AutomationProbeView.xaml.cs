namespace Bruinet.Samples.Maui.App.Views2.TestViews;

public partial class AutomationProbeView : ContentView
{
    public AutomationProbeView()
    {
        InitializeComponent();

        // The gesture-bridge row needs a command to bind to: a TapGestureRecognizer's Tapped
        // event is raised by MAUI internals the bridge does not reach, so only its Command can
        // be driven from outside. The rest of the page binds to nothing, which is why this is
        // set here rather than declared in markup.
        BindingContext = _probe;
    }

    private readonly ProbeGestureModel _probe = new();

    /// <summary>
    /// The gesture-bridge row's command and the text that says whether it ran.
    /// </summary>
    /// <remarks>
    /// Deliberately tiny and local. This page is a measurement instrument, and a shared view
    /// model would let another page's state reach a reading taken here.
    /// </remarks>
    private sealed class ProbeGestureModel : ParentViewModel
    {
        private string _status = "Untouched";

        public ProbeGestureModel()
            => ProbeTapCommand = new RelayCommand(() => ProbeTapStatus = "Tapped");

        public ICommand ProbeTapCommand { get; }

        public string ProbeTapStatus
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }
    }

    private void OnGoToContainer(object? sender, EventArgs e) => Go("ContainerPage");

    private void OnGoToCollection(object? sender, EventArgs e) => Go("CollectionModulePage");

    private void OnGoToShapes(object? sender, EventArgs e) => Go("ShapesPage");

    private void OnGoToDialogs(object? sender, EventArgs e) => Go("DialogsPage");

    /// <summary>
    /// Navigates by Shell route. These pages have no tab of their own - see the module
    /// navigation comment in the XAML.
    /// </summary>
    private static void Go(string route) => _ = Shell.Current?.GoToAsync(route);
}
