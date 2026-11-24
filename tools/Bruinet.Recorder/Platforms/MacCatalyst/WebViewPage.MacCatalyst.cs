using Bruinet.Scraper.Record;
using Foundation;
using WebKit;

namespace Bruinet.Recorder;

// WKWebView: the watcher is a user script at document start, and posts to the script message handler "bruinet".
public sealed partial class WebViewPage
{
    private WKWebView? _native;
    private IDisposable? _addressObserver;

    public partial Task AttachAsync(WebView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _native = view.Handler?.PlatformView as WKWebView
            ?? throw new InvalidOperationException("The webview has no WKWebView yet: attach once it is on the page.");
        var content = _native.Configuration.UserContentController;
        content.AddUserScript(new WKUserScript(new NSString(RecorderScripts.Watcher(RecorderScripts.PostWebKit)), WKUserScriptInjectionTime.AtDocumentStart, isForMainFrameOnly: false));
        content.AddScriptMessageHandler(new Listener(this), "bruinet");
        // The address, also when a single-page app only wrote it: WKWebView's URL is observable.
        _addressObserver = _native.AddObserver("URL", NSKeyValueObservingOptions.New, _ => At(_native.Url?.AbsoluteString));
        return Task.CompletedTask;
    }

    private async partial Task RunHereAsync(string script)
    {
        if (_native is null)
            return;
        try
        {
            await _native.EvaluateJavaScriptAsync(script);
        }
        catch (NSErrorException)
        {
            // The page is navigating or gone, or the script gave something that is no value: nothing to do.
        }
    }

    private sealed class Listener(WebViewPage page) : NSObject, IWKScriptMessageHandler
    {
        public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message) =>
            page.Posted(message.Body as NSString);
    }
}
