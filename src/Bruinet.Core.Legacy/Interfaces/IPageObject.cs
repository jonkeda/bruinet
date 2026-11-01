namespace Bruinet.Core.Interfaces;

/// <summary>
/// Base page interface (non-generic).
/// Represents a page, screen, or view in the application under test.
/// </summary>
public interface IPageObject : IElementScope
{
    /// <summary>
    /// The name of this page for logging and identification.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Check instantaneously whether the current usable page root exists.
    /// </summary>
    bool IsLoaded(int? timeoutMs = null);

    /// <summary>
    /// Probe page identity and activity instantaneously without normal control APIs.
    /// </summary>
    PageReadinessSnapshot ProbeReadiness()
        => new(
            Name,
            IsLoaded() ? PageReadinessState.Ready : PageReadinessState.MissingRoot,
            BusySignalPolicy.Disabled);

    /// <summary>
    /// Check instantaneously whether the current page reports active work.
    /// </summary>
    bool IsBusy() => false;

    /// <summary>
    /// Wait until page busy state matches expected value.
    /// </summary>
    bool WaitBusy(bool? expected, int? timeoutMs = null)
        => expected == null || expected == false;

    /// <summary>
    /// Wait until page loaded state matches expected value.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitLoaded(bool? expected, int? timeoutMs = null);

    /// <summary>
    /// Assert page loaded state matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    void AssertLoaded(bool? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Get the page title.
    /// </summary>
    string? GetTitle(int? timeoutMs = null);

    /// <summary>
    /// Wait until page title matches expected value.
    /// If expected is null, returns true immediately (skip).
    /// </summary>
    bool WaitTitle(string? expected, int? timeoutMs = null);

    /// <summary>
    /// Assert page title matches expected value.
    /// If expected is null, returns immediately (skip).
    /// </summary>
    void AssertTitle(string? expected, string? message = null, int? timeoutMs = null);

    /// <summary>
    /// Take a screenshot of the current page.
    /// </summary>
    void TakeScreenshot(string? filename = null, int? timeoutMs = null);
}

/// <summary>
/// Generic page interface with typed element finding.
/// TElement is the platform's native element type.
/// </summary>
public interface IPageObject<TElement> : IPageObject, IElementScope<TElement>
{
}
