#if !WINDOWS && !MACCATALYST
namespace Bruinet.Recorder;

// Where there is no webview the Recorder knows (the plain net10.0 build): the project builds, and attaching says why not.
public sealed partial class WebViewPage
{
    public partial Task AttachAsync(WebView view) =>
        throw new PlatformNotSupportedException("The Recorder runs on Windows (WebView2) and on a Mac (WKWebView).");

    private partial Task RunHereAsync(string script) => Task.CompletedTask;
}
#endif
