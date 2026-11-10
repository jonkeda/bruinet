namespace Bruinet.Scraper.Snapshots;

/// <summary>
/// Where the Scraper records from: a browser for Html, and later an app's UI Automation tree. One per
/// recording session. Node numbers in <see cref="HighlightAsync"/> and <see cref="PickAsync"/> are
/// those of the last <see cref="CaptureAsync"/>.
/// </summary>
public interface IScrapeSource : IAsyncDisposable
{
    /// <summary>The platform name its snapshots carry.</summary>
    string Platform { get; }

    /// <summary>Raised when the page or window changed and has settled: a snapshot can be taken.</summary>
    event EventHandler<ScrapeTransition>? Transitioned;

    /// <summary>
    /// Raised, in order, for each action the person takes while actions are recorded. An action on an
    /// element that came after the last capture carries a capture taken for it.
    /// </summary>
    event EventHandler<ScrapeAction>? Acted;

    /// <summary>Starts raising <see cref="Acted"/>. A secret field's value is not read: its action carries the placeholder.</summary>
    /// <param name="options">The test id attributes and the secret fields.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    Task StartActionsAsync(ScrapeActionOptions options, CancellationToken cancellationToken = default);

    /// <summary>Stops raising <see cref="Acted"/>.</summary>
    void StopActions();

    /// <summary>Raised when the person uses the test recording's toolbar: <c>assert</c> or <c>stop</c>.</summary>
    event EventHandler<string>? Commanded;

    /// <summary>
    /// Lets the person click an element of the last capture, then choose a check of what it shows; null
    /// when they cancel.
    /// </summary>
    Task<ScrapeAssert?> PickAssertAsync(CancellationToken cancellationToken = default);

    /// <summary>Shows how many actions are recorded on the toolbar.</summary>
    Task ShowCountAsync(int actions, CancellationToken cancellationToken = default);

    /// <summary>Opens the app or page to record.</summary>
    Task StartAsync(ScrapeStart start, CancellationToken cancellationToken = default);

    /// <summary>Waits until the page or window has settled, then captures it.</summary>
    Task<ScrapeSnapshot> CaptureAsync(CancellationToken cancellationToken = default);

    /// <summary>A PNG of what the app shows now, or null when the source cannot take one.</summary>
    Task<byte[]?> ScreenshotAsync(CancellationToken cancellationToken = default);

    /// <summary>Outlines a node in the app, or removes the outline when <paramref name="number"/> is null.</summary>
    Task HighlightAsync(int? number, CancellationToken cancellationToken = default);

    /// <summary>Lets the user click a node in the app; returns its number, or null when they cancel.</summary>
    Task<int?> PickAsync(CancellationToken cancellationToken = default);
}

/// <summary>What a recording starts on.</summary>
/// <param name="Location">The page's address, or the app to start.</param>
/// <param name="ProfileFolder">Where the source keeps its login and cookies between sessions; outside the repository.</param>
/// <param name="Visible">True for a window the user works in; false only for a capture of our own sites in tests.</param>
public sealed record ScrapeStart(string Location, string ProfileFolder, bool Visible = true);

/// <summary>The page or window changed.</summary>
/// <param name="Location">Where it is now.</param>
public sealed record ScrapeTransition(string Location);
