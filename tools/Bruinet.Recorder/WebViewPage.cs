using Bruinet.Scraper.Record;

namespace Bruinet.Recorder;

/// <summary>
/// The webview as the Recorder's engine sees it. What differs per platform is in the platform's part: adding the
/// watcher to every document, receiving what it posts, and running a script.
/// </summary>
public sealed partial class WebViewPage : IRecorderPage
{
    /// <inheritdoc/>
    public event EventHandler<string>? Message;

    /// <summary>Raised when the page is at another address, also one a single-page app only wrote in the address bar.</summary>
    public event EventHandler<string>? AddressChanged;

    /// <summary>Makes the platform's webview ready: after this, a page that opens has the watcher. Call once, before the first page.</summary>
    public partial Task AttachAsync(WebView view);

    /// <inheritdoc/>
    public Task RunAsync(string script, CancellationToken cancellationToken = default) =>
        // The engine calls from a thread of its own; a webview is only spoken to where the window lives.
        MainThread.IsMainThread ? RunHereAsync(script) : MainThread.InvokeOnMainThreadAsync(() => RunHereAsync(script));

    private partial Task RunHereAsync(string script);

    private void Posted(string? message)
    {
        if (!string.IsNullOrEmpty(message))
            Message?.Invoke(this, message);
    }

    private void At(string? address)
    {
        if (!string.IsNullOrEmpty(address))
            AddressChanged?.Invoke(this, address);
    }
}
