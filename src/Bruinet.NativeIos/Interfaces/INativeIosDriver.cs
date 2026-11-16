using Bruinet.Core.Interfaces;
using Bruinet.Core.Recording;

namespace Bruinet.NativeIos.Interfaces;

/// <summary>
/// The Appium session on one iOS simulator or device: the app under test, the screen, and the
/// buttons and links a test gives it. Lookups make one attempt; the call around them is what waits.
/// </summary>
public interface INativeIosDriver : IDiagnosticDriver, IDisposable
{
    /// <summary>The app under test's bundle id; null when the session names none.</summary>
    string? BundleId { get; }

    /// <summary>The bundle id of the app in front now (<c>com.apple.springboard</c> for a system alert).</summary>
    string? CurrentBundleId { get; }

    /// <summary>Whether the software keyboard is shown.</summary>
    bool IsKeyboardShown { get; }

    /// <summary>The simulator, for the screen recorder; null once the session is gone or on a device.</summary>
    RecordingTarget? RecordingTarget { get; }

    /// <summary>Whether the app under test is running in the foreground.</summary>
    bool IsAppInForeground();

    /// <summary>
    /// The text of the alert iOS shows over the app now, or null when there is none. It sees alerts
    /// that are in neither the app's tree nor Springboard's, such as AutoFill's "Save Password?".
    /// One attempt.
    /// </summary>
    string? SystemAlertText { get; }

    /// <summary>The elements that match now, on the whole screen. One attempt.</summary>
    IReadOnlyList<INativeIosElement> FindElements(Locator locator);

    /// <summary>
    /// The elements that match now in Springboard's tree: a privacy prompt is drawn there and is not in
    /// the app's tree. The elements stay usable afterwards; lookups inside them work as usual. One attempt.
    /// </summary>
    IReadOnlyList<INativeIosElement> FindSystemElements(Locator locator);

    /// <summary>
    /// Scrolls the first scrollable element until one matching <paramref name="locator"/> is shown.
    /// One attempt; false when nothing scrolls or the element did not come into view.
    /// </summary>
    bool TryScrollIntoView(Locator locator);

    /// <summary>
    /// Scrolls <paramref name="scrollable"/> until an element matching <paramref name="locator"/> is
    /// shown in it. One attempt of at most 30 s; false when no such element is in the container's tree
    /// (a collection view keeps only the cells on the screen) or it did not come into view.
    /// </summary>
    bool TryScrollIntoView(Locator locator, Locator scrollable);

    /// <summary>Opens <paramref name="uri"/> in the app under test.</summary>
    void OpenDeepLink(string uri);

    /// <summary>Goes back: taps the navigation bar's back button. iOS has no system Back key.</summary>
    void Back();

    /// <summary>Presses a hardware button: <c>home</c>, <c>volumeup</c> or <c>volumedown</c>.</summary>
    void PressButton(string name);

    /// <summary>Hides the software keyboard when it is shown.</summary>
    void HideKeyboard();

    /// <summary>Brings the app under test to the front, starting it when it is not running.</summary>
    void ActivateApp();

    /// <summary>Stops the app under test.</summary>
    void TerminateApp();

    /// <summary>Stops and restarts the app under test.</summary>
    void ResetAppState();

    /// <summary>The screen as PNG bytes.</summary>
    byte[] TakeScreenshot();

    /// <summary>Saves the screen as a PNG file.</summary>
    void SaveScreenshot(string path);
}
