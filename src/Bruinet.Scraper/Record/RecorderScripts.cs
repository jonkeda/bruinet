using System.Text.Json;
using Bruinet.Scraper.Html;

namespace Bruinet.Scraper.Record;

/// <summary>The scripts the Recorder puts in a page: the Scraper's own capture, and the watcher that says when to take one.</summary>
public static class RecorderScripts
{
    /// <summary>How the watcher posts a message in WebView2 (Windows).</summary>
    public const string PostWebView2 = "message => window.chrome.webview.postMessage(message)";

    /// <summary>How the watcher posts a message in WKWebView (Mac), to the script message handler <c>bruinet</c>.</summary>
    public const string PostWebKit = "message => window.webkit.messageHandlers.bruinet.postMessage(message)";

    /// <summary>
    /// The script a host adds to every document, before the page's own: it posts a <c>hello</c>, and once turned on
    /// (<see cref="Set"/>) each press and each time the page goes still, with the page as <c>capture.js</c> reads it.
    /// </summary>
    /// <param name="post">A script function that hands one text to the host (<see cref="PostWebView2"/>, <see cref="PostWebKit"/>).</param>
    public static string Watcher(string post)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(post);
        var kept = JsonSerializer.Serialize(HtmlAttributes.Kept);
        var quiet = (int)PlaywrightScrapeSource.Quiet.TotalMilliseconds;
        var max = (int)PlaywrightScrapeSource.SettleLimit.TotalMilliseconds;
        return $"({Script("watch.js")})({{ post: {post}, capture: {Script("capture.js")}, kept: {kept}, maxText: {PlaywrightScrapeSource.MaxText}, quiet: {quiet}, max: {max} }});";
    }

    /// <summary>The script that turns the watcher of the open document on or off, with values or without.</summary>
    public static string Set(bool on, bool values) =>
        $"window.__bruinetRecorder && window.__bruinetRecorder.set({(on ? "true" : "false")}, {(values ? "true" : "false")})";

    private static string Script(string name)
    {
        using var stream = typeof(RecorderScripts).Assembly.GetManifestResourceStream($"Bruinet.Scraper.Html.Scripts.{name}")
            ?? throw new InvalidOperationException($"The script {name} is not in {typeof(RecorderScripts).Assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
