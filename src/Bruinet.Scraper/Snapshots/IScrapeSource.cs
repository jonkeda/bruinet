namespace Bruinet.Scraper.Snapshots;

/// <summary>
/// Where the Scraper records from: a browser for Html, and later an app's UI Automation tree. One per
/// recording session.
/// </summary>
public interface IScrapeSource : IAsyncDisposable
{
    /// <summary>The platform name its snapshots carry.</summary>
    string Platform { get; }

    /// <summary>Raised when the page or window changed and has settled: a snapshot can be taken.</summary>
    event EventHandler<ScrapeTransition>? Transitioned;

    /// <summary>Opens the app or page to record.</summary>
    Task StartAsync(ScrapeStart start, CancellationToken cancellationToken = default);

    /// <summary>Waits until the page or window has settled, then captures it.</summary>
    Task<ScrapeSnapshot> CaptureAsync(CancellationToken cancellationToken = default);

    /// <summary>A PNG of what the app shows now, or null when the source cannot take one.</summary>
    Task<byte[]?> ScreenshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>What a recording starts on.</summary>
/// <param name="Location">The page's address, or the app to start.</param>
/// <param name="ProfileFolder">Where the source keeps its login and cookies between sessions; outside the repository.</param>
/// <param name="Visible">True for a window the user works in; false only for a capture of our own sites in tests.</param>
public sealed record ScrapeStart(string Location, string ProfileFolder, bool Visible = true)
{
    /// <summary>
    /// A browser's saved sign-in (cookies and storage, as Playwright writes it) loaded into the profile before the
    /// first page, so the source starts signed in without anyone typing a credential; null for none.
    /// </summary>
    public string? SignInState { get; init; }
}

/// <summary>The page or window changed.</summary>
/// <param name="Location">Where it is now.</param>
public sealed record ScrapeTransition(string Location);
