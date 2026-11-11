using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Maui.Containers;
using Bruinet.Maui.Controls;

namespace Bruinet.Maui.Pages;

/// <summary>
/// Base class for MAUI page objects with fluent method chaining support.
/// Uses CRTP (Curiously Recurring Template Pattern) for strongly-typed fluent returns.
/// Pages are root containers: their root is found from the driver and all child controls
/// resolve strictly within that root.
/// </summary>
/// <typeparam name="TSelf">The concrete page type (CRTP pattern).</typeparam>
public abstract class PageObjectBase<TSelf> : RootedScopeBase<TSelf, TSelf>, IMauiPage<TSelf>
    where TSelf : PageObjectBase<TSelf>
{
    private readonly IMauiTestContext _context;
    private const string DefaultBusyAutomationId = "Busy";

    // The busy label found under the page root, kept while that root is: each call re-found it,
    // one request in every page check. Its value is read live; a stale label is found again.
    private IMauiElement? _busyRoot;
    private IMauiElement? _busySignal;

    /// <summary>
    /// Creates a new page object with the specified context.
    /// </summary>
    /// <param name="context">The MAUI test context.</param>
    protected PageObjectBase(IMauiTestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public override IMauiTestContext Context => _context;

    #region Page state

    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    protected override Locator Locator => new(LocatorStrategy.AutomationId, Name);

    public override IMauiPage? Page => this;

    /// <remarks>
    /// The first match is nearly always the usable one, and finding it stops early; collecting
    /// every match walks the whole app, so it is kept for when the first has no bounds.
    /// </remarks>
    protected override IMauiElement FindContainerRootElement()
        => Context.TryFindElement(Locator) is { } first && first.HasUsableBounds()
            ? first
            : Context.FindElements(Locator).FirstOrDefault(element => element.HasUsableBounds())
              ?? throw RootNotFound();

    /// <inheritdoc />
    protected override ElementNotFoundException RootNotFound()
        => new($"Page root not found. Locator: {Locator}");

    /// <remarks>
    /// Attached as well as shown (call model R4, R6). On Windows a page Shell navigated away from
    /// keeps answering as present, on screen and full size for about a second after it left the
    /// tree, so a cached root that only had to be shown went on reading the page underneath.
    /// </remarks>
    protected override bool IsCachedRootValid(IMauiElement root)
        => root.HasUsableBounds()
           && (root is not IDetachable detachable
               || Bruinet.Core.Calls.AttemptMemo.Remember(root, nameof(IDetachable.IsAttached), () => detachable.IsAttached));

    protected override TSelf SetResult => Self;

    /// <summary>How this page exposes its page-local busy state.</summary>
    protected virtual BusySignalPolicy BusySignalPolicy => BusySignalPolicy.Disabled;

    /// <summary>The AutomationId of the page-local busy signal.</summary>
    protected virtual string BusyAutomationId => DefaultBusyAutomationId;

    /// <inheritdoc />
    protected override string ScopeName => Name;

    /// <inheritdoc />
    /// <remarks>
    /// The root is only ever handed back with usable bounds - a cached one is re-checked by
    /// <see cref="IsCachedRootValid"/>, a fresh one is chosen by them - so asking again here
    /// repeated the same two reads. On Android a visibility read is 57 ms and this runs on every
    /// readiness probe.
    /// </remarks>
    public virtual bool IsLoaded()
        => TryGetContainerRoot() is not null;

    /// <inheritdoc />
    protected override ScopeReadiness ProbeContentReadiness(IMauiElement root)
    {
        if (!IsLoaded())
            return new ScopeReadiness(Name, ScopeReadinessState.NotLoaded);

        if (BusySignalPolicy == BusySignalPolicy.Disabled)
            return ContentReady();

        if (ReferenceEquals(root, _busyRoot) && _busySignal is { } kept)
        {
            try
            {
                return BusyAnswer(kept.Text);
            }
            catch (StaleElementException)
            {
                _busyRoot = null;
                _busySignal = null;
            }
        }

        var signal = root.TryFindElement(Locator.ByAutomationId(BusyAutomationId));
        // A full-screen loading view can hide the whole page, signal included, between the root
        // check and the search: that page is not loaded yet, which a later probe sees. The root is
        // read afresh, as the attempt's earlier answer predates the search.
        if (signal == null && !root.HasUsableBoundsNow())
            return new ScopeReadiness(Name, ScopeReadinessState.NotLoaded, "page hidden while it loads");
        if (signal == null)
            return new ScopeReadiness(
                Name,
                ScopeReadinessState.MissingBusySignal,
                $"Page '{Name}' requires a page-local busy signal with AutomationId:{BusyAutomationId}, but it was not found beneath the current page root.");

        var value = signal.Text;
        _busyRoot = root;
        _busySignal = signal;
        return BusyAnswer(value);
    }

    private ScopeReadiness BusyAnswer(string? value)
    {
        if (!bool.TryParse(value, out var busy))
            return new ScopeReadiness(
                Name,
                ScopeReadinessState.InvalidBusySignal,
                $"Page '{Name}' busy signal AutomationId:{BusyAutomationId} must contain 'True' or 'False', but contained '{value ?? "(null)"}'.");

        return busy
            ? new ScopeReadiness(Name, ScopeReadinessState.Busy, $"busy value: '{value}'")
            : ContentReady();
    }

    /// <inheritdoc />
    protected override ScopeReadiness ProbeCallReadiness() => ProbeReadiness();

    /// <inheritdoc />
    protected override int DefaultReadyTimeoutMs => Context.Timeouts.PageLoad.ToMs();

    /// <inheritdoc />
    public bool IsBusy()
    {
        var readiness = ProbeReadiness();
        if (readiness.IsConfigurationError)
            throw ScopeGate.Misconfigured(readiness);

        return readiness.IsBusy;
    }

    /// <inheritdoc />
    public bool WaitBusy(bool? expected = true, int? timeoutMs = null)
    {
        if (expected == null)
            return true;

        return RunProbe(() =>
        {
            var readiness = ProbeReadiness();
            if (readiness.IsConfigurationError)
                throw ScopeGate.Misconfigured(readiness);

            return readiness.IsLoaded && readiness.IsBusy == expected.Value
                ? Observation.Done()
                : Observation.ScopeNotReady(readiness);
        }, timeoutMs ?? Context.Timeouts.PageLoad.ToMs());
    }

    /// <summary>Waits until the loaded page is not busy: <c>WaitReady</c>.</summary>
    public bool WaitIdle(int? timeoutMs = null) => WaitReady(timeoutMs);

    /// <summary>
    /// Asserts that the page becomes ready: loaded and not busy.
    /// </summary>
    /// <param name="message">Optional custom failure message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <exception cref="ScopeNotReadyException">The page did not become ready within the timeout.</exception>
    public void AssertIdle(string? message = null, int? timeoutMs = null)
    {
        var budget = timeoutMs ?? Context.Timeouts.PageLoad.ToMs();
        RunProbe(
            () => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(),
            budget,
            readiness => new ScopeNotReadyException(
                readiness,
                message ?? $"Page '{Name}' did not become ready within {budget} ms. Last readiness state: {readiness.State}" +
                    (readiness.Detail is null ? "." : $" ({readiness.Detail}).")));
    }

    /// <inheritdoc />
    public bool WaitLoaded(bool? expected = true, int? timeoutMs = null)
    {
        if (expected == null)
            return true;

        return RunProbe(
            () => IsLoaded() == expected.Value ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? Context.Timeouts.PageLoad.ToMs());
    }

    /// <inheritdoc />
    /// <exception cref="ScopeNotReadyException">The loaded state did not match within the timeout.</exception>
    public void AssertLoaded(bool? expected = true, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return;

        RunProbe(
            () => IsLoaded() == expected.Value ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? Context.Timeouts.PageLoad.ToMs(),
            readiness => new ScopeNotReadyException(
                readiness,
                message ?? $"Expected page '{Name}' {(expected.Value ? "to be loaded" : "not to be loaded")} but it is {(expected.Value ? "not" : "still loaded")}. Last readiness state: {readiness.State}."));
    }

    /// <inheritdoc />
    public virtual string? GetTitle()
    {
        return Name;
    }

    /// <inheritdoc />
    public bool WaitTitle(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;

        return RunProbe(
            () => GetTitle() == expected ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? Context.Timeouts.DefaultWait.ToMs());
    }

    /// <inheritdoc />
    /// <exception cref="ControlAssertionException">The title did not match within the timeout.</exception>
    public void AssertTitle(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return;

        RunProbe(
            () => GetTitle() == expected ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? Context.Timeouts.DefaultWait.ToMs(),
            _ =>
            {
                var actual = GetTitle();
                return new ControlAssertionException(
                    message ?? $"Expected page title '{expected}'.", expected, actual);
            });
    }

    /// <inheritdoc />
    public void TakeScreenshot(string? filename = null)
    {
        var path = filename ?? $"{Name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        Context.SaveScreenshot(path);
    }

    #endregion
}
