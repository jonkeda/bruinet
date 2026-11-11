using System.Diagnostics;
using Bruinet.Core.Recording;
using Bruinet.FlaUI;

namespace Bruinet.WinForms.FlaUI;

/// <summary>
/// The WinForms app under test through FlaUI's UI Automation, over a <see cref="DesktopApp"/>: launched
/// with the foreground handed back and its window placed as asked, or attached to.
/// </summary>
internal sealed class FlaUIWinFormsDriver : IWinFormsDriver, IRecordable
{
    private readonly DesktopApp _app;

    /// <summary>The driver over an app session.</summary>
    public FlaUIWinFormsDriver(DesktopApp app)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
    }

    /// <summary>Launches the app at <paramref name="executablePath"/>.</summary>
    /// <param name="executablePath">The app's executable.</param>
    /// <param name="arguments">Its command line, or null.</param>
    /// <param name="environment">Variables for the app's process only, or null.</param>
    /// <param name="startTimeout">How long the app may take to be ready and show its main window; 30 s when null.</param>
    public static FlaUIWinFormsDriver Launch(string executablePath, string? arguments = null, IReadOnlyDictionary<string, string>? environment = null, TimeSpan? startTimeout = null)
        => new(DesktopApp.Launch(executablePath, arguments, environment, startTimeout));

    /// <summary>Attaches to the window <paramref name="windowHandle"/>.</summary>
    public static FlaUIWinFormsDriver Attach(IntPtr windowHandle) => new(DesktopApp.Attach(windowHandle));

    /// <summary>Attaches to the running process <paramref name="process"/>.</summary>
    public static FlaUIWinFormsDriver Attach(Process process) => new(DesktopApp.Attach(process));

    /// <summary>The app session the driver reads.</summary>
    public DesktopApp App => _app;

    /// <inheritdoc />
    public IWinFormsElement Window => new FlaUIWinFormsElement(_app.RootElement, _app);

    /// <inheritdoc />
    public string CurrentWindowHandle => _app.WindowHandle.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public bool HasExited => _app.HasExited;

    /// <inheritdoc />
    public RecordingTarget? RecordingTarget => _app.HasExited ? null : RecordingTarget.ForWindow(CurrentWindowHandle);

    /// <inheritdoc />
    public IReadOnlyList<IWinFormsElement> FindElements(Locator locator)
        => UiaElements.Live<IReadOnlyList<IWinFormsElement>>(_app, () =>
            [.. UiaElements.FindAll(_app, _app.RootElement, locator).Select(found => new FlaUIWinFormsElement(found, _app))]);

    /// <inheritdoc />
    public IReadOnlyList<IWinFormsElement> TopLevelWindows()
        => [.. _app.TopLevelWindows().Select(window => new FlaUIWinFormsElement(window, _app))];

    /// <inheritdoc />
    public void Close() => _app.CloseWindow();

    /// <inheritdoc />
    public void Quit() => _app.CloseApp();

    /// <inheritdoc />
    public byte[] TakeScreenshot() => _app.Screenshot();

    /// <inheritdoc />
    public string GetPageSource() => _app.AutomationTree();

    /// <inheritdoc />
    public string GetAutomationTree() => _app.AutomationTree();

    /// <inheritdoc />
    public void Dispose() => _app.Dispose();
}
