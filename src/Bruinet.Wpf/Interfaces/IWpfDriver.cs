namespace Bruinet.Wpf.Interfaces;

/// <summary>
/// The WPF app under test through UI Automation: its main window, the windows it opens, and its
/// screen. Lookups make one attempt; the call around them is what waits (R2).
/// </summary>
public interface IWpfDriver : IDiagnosticDriver, IDisposable
{
    /// <summary>The app's main window, as an element.</summary>
    IWpfElement Window { get; }

    /// <summary>The main window's native handle, as text.</summary>
    string CurrentWindowHandle { get; }

    /// <summary>Whether the app's process has exited; false when the process is not known.</summary>
    bool HasExited { get; }

    /// <summary>The elements under the main window that match now. One attempt.</summary>
    IReadOnlyList<IWpfElement> FindElements(Locator locator);

    /// <summary>The app's top-level windows now: the main window and any it opened (a dialog that is not owned by it).</summary>
    IReadOnlyList<IWpfElement> TopLevelWindows();

    /// <summary>Closes the main window through its Window pattern.</summary>
    void Close();

    /// <summary>Closes the app.</summary>
    void Quit();

    /// <summary>The main window as PNG bytes.</summary>
    byte[] TakeScreenshot();
}
