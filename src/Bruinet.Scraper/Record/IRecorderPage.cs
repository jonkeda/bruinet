namespace Bruinet.Scraper.Record;

/// <summary>
/// The page a person browses, as the Recorder needs it: a webview of an app, or a browser in a test. The host adds
/// <see cref="RecorderScripts.Watcher"/> to every document before the page's own scripts run, and raises
/// <see cref="Message"/> with what that script posts.
/// </summary>
public interface IRecorderPage
{
    /// <summary>Raised with each message (a JSON text) the watcher posted.</summary>
    event EventHandler<string>? Message;

    /// <summary>Runs a script in the page's top document. A page that is gone or navigating is no error.</summary>
    Task RunAsync(string script, CancellationToken cancellationToken = default);
}
