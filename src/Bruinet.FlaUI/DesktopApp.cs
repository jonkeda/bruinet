using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using Bruinet.Core.Diagnostics;
using Bruinet.FlaUI.Windowing;
using FlaUI.Core.Capturing;

namespace Bruinet.FlaUI;

/// <summary>
/// A desktop app under test, as UI Automation sees it: the process (when it was launched or
/// attached by process), its top-level window, and the automation that reads it. What every
/// FlaUI stack's driver needs whatever the app is made with: a launch that hands the foreground
/// back, the requested window placement, a root window that is attached again when UI Automation
/// retires it, screenshots of the window's own content, the automation tree, and closing.
/// </summary>
public sealed class DesktopApp : IDisposable
{
    private readonly Application? _application;
    private readonly AppWindow _window;
    private readonly QuietWindow? _quiet;
    private bool _disposed;

    private DesktopApp(UIA3Automation automation, Application? application, AppWindow window, QuietWindow? quiet)
    {
        Automation = automation;
        _application = application;
        _window = window;
        _quiet = quiet;
        ConditionFactory = new ConditionFactory(automation.PropertyLibrary);
    }

    // How long a launched app may take to be ready for input and show its main window.
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(30);

    // The least the main-window search is given when becoming ready used most of the launch's time.
    private static readonly TimeSpan MinimumWindowWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Launches the app and attaches to its main window.
    /// </summary>
    /// <param name="executablePath">Path to the application executable.</param>
    /// <param name="arguments">Command line arguments, or null.</param>
    /// <param name="environment">
    /// Variables set on the launched process only: the test process's own environment is not
    /// changed, so each launch carries its own settings (a stack's launch settings, a bridge's
    /// switch, <c>BRUINET_AUT_PLACE</c>).
    /// </param>
    /// <returns>The app, its main window found.</returns>
    /// <exception cref="InvalidOperationException">The process did not start, was not ready for input, or showed no main window, within 30 s of starting.</exception>
    public static DesktopApp Launch(string executablePath, string? arguments = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        var automation = new UIA3Automation();

        var processStartInfo = new ProcessStartInfo(executablePath)
        {
            Arguments = arguments ?? string.Empty,

            // FALSE, AND THIS MATTERS MORE THAN IT LOOKS.
            //
            // With UseShellExecute the launch goes through the shell, and what the child
            // inherits is not reliably this process's environment. Measured on MAUI: roughly one
            // run in six came up without the variables it was given - every test failing, for
            // seventeen seconds at a time, against an app that was running perfectly well.
            UseShellExecute = false,
        };

        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            processStartInfo.Environment[name] = value;
        }

        // Whoever the user was working in before the run. Windows hands a freshly launched
        // process the foreground; QuietWindow hands it back.
        var previousForeground = NativeMethods.GetForegroundWindow();

        var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {executablePath}");

        // Before anything waits on the app - see QuietWindow.ForLaunch. An app someone is watching
        // (BRUINET_AUT_WINDOW=shown, the Presenter) is left where it can be seen.
        var quiet = AutWindow.IsShown(environment) ? null : QuietWindow.ForLaunch(process.Id, previousForeground);

        // A test being debugged: the IDE attaches to the app too.
        DebuggedApps.Announce(process, Path.GetFileNameWithoutExtension(executablePath));

        // One budget for the whole launch: ready for input, then a main window, within LaunchTimeout.
        var app = Path.GetFileName(executablePath);
        var launched = Stopwatch.StartNew();
        if (!process.WaitForInputIdle(LaunchTimeout))
        {
            throw new InvalidOperationException($"'{app}' was not ready for input within {LaunchTimeout.TotalSeconds:0} s of starting.");
        }

        var application = Application.Attach(process);
        var left = LaunchTimeout - launched.Elapsed;
        var main = application.GetMainWindow(automation, left > MinimumWindowWait ? left : MinimumWindowWait)
            ?? throw new InvalidOperationException($"'{app}' showed no main window within {LaunchTimeout.TotalSeconds:0} s of starting.");
        var window = new AppWindow(automation, main);

        WindowPlacement.ApplyRequested(window, environment);
        quiet?.Settle(window.Handle);

        return new DesktopApp(automation, application, window, quiet);
    }

    /// <summary>Attaches to an existing window by its handle; the app's process is not known.</summary>
    /// <param name="windowHandle">The window handle to attach to.</param>
    public static DesktopApp Attach(IntPtr windowHandle)
    {
        var automation = new UIA3Automation();
        return new DesktopApp(automation, null, AppWindow.FromHandle(automation, windowHandle), null);
    }

    /// <summary>Attaches to a running process and its main window.</summary>
    /// <param name="process">The process to attach to.</param>
    /// <exception cref="InvalidOperationException">The process showed no main window within 30 s.</exception>
    public static DesktopApp Attach(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        var automation = new UIA3Automation();
        var application = Application.Attach(process);
        var main = application.GetMainWindow(automation, TimeSpan.FromSeconds(30))
            ?? throw new InvalidOperationException($"The process '{process.ProcessName}' ({process.Id}) showed no main window within 30 s.");
        return new DesktopApp(automation, application, new AppWindow(automation, main), null);
    }

    /// <summary>The UI Automation instance the app is read with.</summary>
    public UIA3Automation Automation { get; }

    /// <summary>Builds search conditions for <see cref="Automation"/>.</summary>
    public ConditionFactory ConditionFactory { get; }

    /// <summary>The app's top-level window, attached again by its handle if UI Automation has retired it.</summary>
    public AutomationElement RootElement => _window.Element;

    /// <summary>The top-level window's native handle; zero when UI Automation reported none.</summary>
    public IntPtr WindowHandle => _window.Handle;

    /// <summary>Whether the app's process is known (launched, or attached by process).</summary>
    public bool HasProcess => _application != null;

    /// <summary>Whether the app's process has exited; false when the process is not known.</summary>
    public bool HasExited
    {
        get
        {
            try
            {
                return _application?.HasExited == true;
            }
            catch (InvalidOperationException)
            {
                // The process object no longer answers; it is gone.
                return true;
            }
        }
    }

    /// <summary>How many times the root element had gone stale and was attached again.</summary>
    public int RootReattachments => _window.Reattachments;

    /// <summary>How many times the app took the foreground and had to be put back.</summary>
    public int ForegroundGrabs => _quiet?.ForegroundGrabs ?? 0;

    /// <summary>
    /// The app's top-level windows. Walks the desktop and filters by process (seconds on a busy
    /// machine), so it backs up a search inside <see cref="RootElement"/>, never replaces it. Only
    /// the root window when the process is not known.
    /// </summary>
    public IReadOnlyList<AutomationElement> TopLevelWindows()
        => _application != null ? _application.GetAllTopLevelWindows(Automation) : [RootElement];

    /// <summary>The handles of <see cref="TopLevelWindows"/>, as text.</summary>
    public IReadOnlyCollection<string> WindowHandles()
        => TopLevelWindows()
            .Select(w => w.Properties.NativeWindowHandle.Value.ToString(CultureInfo.InvariantCulture))
            .ToList();

    /// <summary>Closes the app's process, when it is known.</summary>
    public void CloseApp() => _application?.Close();

    /// <summary>Closes the top-level window through its Window pattern, when it has one.</summary>
    public void CloseWindow()
    {
        var root = RootElement;
        if (root.Patterns.Window.IsSupported)
        {
            root.Patterns.Window.Pattern.Close();
        }
    }

    /// <summary>
    /// A PNG of the app's window: its own content when it renders it, else the screen under it
    /// (which may catch an overlapping window).
    /// </summary>
    public byte[] Screenshot()
    {
        var windowContent = TryCaptureWindowContent();
        if (windowContent != null)
        {
            using (windowContent)
                return ToPng(windowContent);
        }

        using var capture = Capture.Element(RootElement);
        return ToPng(capture.Bitmap);
    }

    /// <summary>The control tree under the root window as XML-like text, for diagnostics.</summary>
    public string AutomationTree() => BuildAutomationTree(RootElement);

    /// <summary>Hands the foreground watch back, closes the app it launched and the automation.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _quiet?.Dispose();
        _application?.Close();
        Automation.Dispose();
        _disposed = true;
    }

    private Bitmap? TryCaptureWindowContent()
    {
        var bitmap = WindowCapture.TryCapture(_window.Handle);
        if (bitmap == null)
            return null;

        if (!WindowCapture.LooksBlank(bitmap))
            return bitmap;

        // Rendered, but empty: the GPU-composition failure mode. Reading the screen is the
        // better answer even though it may catch an overlapping window.
        bitmap.Dispose();
        return null;
    }

    private static byte[] ToPng(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static string BuildAutomationTree(AutomationElement element, int depth = 0)
    {
        var indent = new string(' ', depth * 2);
        var sb = new StringBuilder();

        // Use safe property access - some elements don't support all properties
        string automationId = "";
        string name = "";
        string className = "";
        string controlType = "Unknown";

        try
        { automationId = element.Properties.AutomationId.ValueOrDefault ?? ""; }
        catch { }
        try
        { name = element.Properties.Name.ValueOrDefault ?? ""; }
        catch { }
        try
        { className = element.Properties.ClassName.ValueOrDefault ?? ""; }
        catch { }
        try
        { controlType = element.ControlType.ToString(); }
        catch { }

        sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}<{controlType} AutomationId=\"{automationId}\" Name=\"{name}\" ClassName=\"{className}\">");

        try
        {
            foreach (var child in element.FindAllChildren())
            {
                sb.Append(BuildAutomationTree(child, depth + 1));
            }
        }
        catch
        {
            // Ignore errors traversing children
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}</{controlType}>");
        return sb.ToString();
    }
}
