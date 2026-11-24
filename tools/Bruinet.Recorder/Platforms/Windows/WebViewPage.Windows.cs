using Bruinet.Scraper.Record;
using Microsoft.Web.WebView2.Core;

namespace Bruinet.Recorder;

// WebView2: the watcher is a script of every new document, and posts through chrome.webview.
public sealed partial class WebViewPage
{
    private CoreWebView2? _core;

    public async partial Task AttachAsync(WebView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var native = view.Handler?.PlatformView as Microsoft.UI.Xaml.Controls.WebView2
            ?? throw new InvalidOperationException("The webview has no WebView2 yet: attach once it is on the page.");
        await native.EnsureCoreWebView2Async();
        _core = native.CoreWebView2;
        await _core.AddScriptToExecuteOnDocumentCreatedAsync(RecorderScripts.Watcher(RecorderScripts.PostWebView2));
        _core.WebMessageReceived += (_, e) => Posted(TryText(e));
        _core.SourceChanged += (_, _) => At(_core.Source);
        // A link that opens a new window is followed here: one page is recorded at a time.
        _core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            _core.Navigate(e.Uri);
        };
    }

    private async partial Task RunHereAsync(string script)
    {
        if (_core is null)
            return;
        try
        {
            await _core.ExecuteScriptAsync(script);
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // The page is navigating or gone: its next document says hello and is told then.
        }
    }

    // A message that is no text is not the watcher's.
    private static string? TryText(CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            return e.TryGetWebMessageAsString();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
