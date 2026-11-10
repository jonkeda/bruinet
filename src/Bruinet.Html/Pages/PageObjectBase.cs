using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Composition;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Scopes;

namespace Bruinet.Html.Pages;

// Test composition and UAT find Html pages by Core's ITestPage, which IHtmlPage derives from.
public abstract class PageObjectBase<TSelf> : ObjectBase, IHtmlPage<TSelf>
    where TSelf : PageObjectBase<TSelf>
{
    /// <summary>The attribute on <c>&lt;body&gt;</c> that carries the busy signal (design 7.2).</summary>
    public const string BusyAttribute = "data-busy";

    private static readonly Locator Body = Locator.ByCss("body");

    private readonly IHtmlTestContext _context;

    protected PageObjectBase(IHtmlTestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    protected PageObjectBase(IHtmlTestContext context, string url)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Url = url;
    }

    public override IHtmlTestContext Context => _context;

    public TSelf Self => (TSelf)this;

    public string? Url { get; }

    public virtual string Name => GetType().Name;

    protected override string ScopeName => Name;

    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    public IHtmlPage? Page => this;

    /// <summary>How this page reads <c>data-busy</c>. Html pages: optional; Blazor pages: required.</summary>
    protected virtual BusySignal BusySignal => BusySignal.Optional;

    protected virtual bool CompareUrl()
    {
        if (Url is null)
        {
            return true;
        }

        var uri = new Uri(_context.CurrentUrl);

        return uri.LocalPath == Url;
    }

    /// <summary>The probe of the attempt <see cref="ProbeReadiness"/> is making, which <see cref="IsLoaded"/> reads.</summary>
    private PageProbe? _probe;

    /// <summary>
    /// Whether the page is loaded now: its URL, and the document loaded (one look, when the
    /// context answers <see cref="IHtmlPageProbe"/>; the browser idle otherwise). The override point.
    /// </summary>
    public virtual bool IsLoaded()
    {
        if (!CompareUrl())
        {
            return false;
        }

        var probe = _probe ?? (_context as IHtmlPageProbe)?.ProbePage(BusyAttribute);
        return probe?.IsComplete ?? _context.IsIdle();
    }

    /// <summary>
    /// One probe of the page (design 7.2): loaded, then the busy signal. A page has no parent to
    /// ask. Where the context answers <see cref="IHtmlPageProbe"/>, the load state and the busy
    /// attribute come from one round trip.
    /// </summary>
    public virtual ScopeReadiness ProbeReadiness()
    {
        var prober = _context as IHtmlPageProbe;
        _probe = prober?.ProbePage(BusyAttribute);
        try
        {
            var readiness = ProbeReadinessOnce();

            // Not loaded or busy on its first look: the context waits in the page for it (one poll interval at most, within
            // the call's budget) and looks again, so the call goes on as the page turns ready.
            if (prober is not null && readiness.State is ScopeReadinessState.NotLoaded or ScopeReadinessState.Busy
                && CompareUrl() && CallBudget.Within(PollingIntervalMs) is var waitMs and > 0)
            {
                _probe = prober.ProbePageWhenReady(BusyAttribute, BusySignal, waitMs);
                readiness = ProbeReadinessOnce();
            }

            return readiness;
        }
        finally
        {
            _probe = null;
        }
    }

    private ScopeReadiness ProbeReadinessOnce()
    {
        if (!IsLoaded())
        {
            return new ScopeReadiness(Name, ScopeReadinessState.NotLoaded);
        }

        if (BusySignal == BusySignal.Ignore)
        {
            return ScopeReadiness.Ready(Name);
        }

        var value = _probe is { } probe ? probe.Busy : _context.TryFindElement(Body)?.GetAttribute(BusyAttribute);
        return value switch
        {
            null when BusySignal == BusySignal.Required
                => new ScopeReadiness(Name, ScopeReadinessState.NotLoaded, $"no {BusyAttribute} on <body> yet: the app is not interactive"),
            null => ScopeReadiness.Ready(Name),
            _ when string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) => ScopeReadiness.Ready(Name),
            _ when string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                => new ScopeReadiness(Name, ScopeReadinessState.Busy, $"{BusyAttribute}: '{value}'"),
            _ => new ScopeReadiness(Name, ScopeReadinessState.InvalidBusySignal,
                $"Page '{Name}': {BusyAttribute} on <body> must be 'true' or 'false', but is '{value}'."),
        };
    }

    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? _context.Timeouts.PageLoad.ToMs());

    public bool WaitLoaded(bool? expected = true, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return true;
        }

        return RunProbe(
            () => IsLoaded() == expected.Value ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.PageLoad.ToMs());
    }

    public void AssertLoaded(bool? expected = true, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return;
        }

        RunProbe(
            () => IsLoaded() == expected.Value ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.PageLoad.ToMs(),
            readiness => new ScopeNotReadyException(
                readiness,
                message ?? $"Expected page '{Name}' {(expected.Value ? "to be loaded" : "not to be loaded")} but it is {(expected.Value ? "not" : "still loaded")}."));
    }

    public bool IsBusy()
    {
        var readiness = ProbeReadiness();
        if (readiness.IsConfigurationError)
        {
            throw ScopeGate.Misconfigured(readiness);
        }

        return readiness.IsBusy;
    }

    public bool WaitBusy(bool? expected = true, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return true;
        }

        return RunProbe(() =>
        {
            var readiness = ProbeReadiness();
            if (readiness.IsConfigurationError)
            {
                throw ScopeGate.Misconfigured(readiness);
            }

            return readiness.IsLoaded && readiness.IsBusy == expected.Value
                ? Observation.Done()
                : Observation.ScopeNotReady(readiness);
        }, timeoutMs ?? _context.Timeouts.PageLoad.ToMs());
    }

    public void AssertIdle(string? message = null, int? timeoutMs = null)
    {
        var budget = timeoutMs ?? _context.Timeouts.PageLoad.ToMs();
        RunProbe(
            () => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(),
            budget,
            readiness => new ScopeNotReadyException(
                readiness,
                message ?? $"Page '{Name}' did not become ready within {budget} ms. Last readiness state: {readiness.State}" +
                    (readiness.Detail is null ? "." : $" ({readiness.Detail}).")));
    }

    public virtual string? GetTitle()
    {
        return _context.PageTitle;
    }

    public bool WaitTitle(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return true;
        }

        return RunProbe(
            () => GetTitle() == expected ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.DefaultWait.ToMs());
    }

    public void AssertTitle(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
        {
            return;
        }

        RunProbe(
            () => GetTitle() == expected ? Observation.Done() : Observation.Pending(),
            timeoutMs ?? _context.Timeouts.DefaultWait.ToMs(),
            _ =>
            {
                var actual = GetTitle();
                return new AssertionException(
                    message ?? $"Expected page title '{expected}' but got '{actual ?? "(null)"}'.", expected, actual);
            });
    }

    public void TakeScreenshot(string? filename = null)
    {
        var path = filename ?? $"{Name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        _context.SaveScreenshot(path);
    }

    public IHtmlElement? TryFindElement(Locator locator)
    {
        return _context.TryFindElement(locator);
    }

    public IHtmlElement FindElement(Locator locator)
    {
        return _context.FindElement(locator);
    }

    public IReadOnlyList<IHtmlElement> FindElements(Locator locator)
    {
        return _context.FindElements(locator);
    }

    protected HtmlTestContextOptions DefaultOptions() => new();
}
