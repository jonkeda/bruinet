using Bruinet.Core.Calls;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Pages;

/// <summary>
/// The whole browser page as a scope, for controls that do not live inside a page object.
/// </summary>
public sealed class AppRoot : ObjectBase, IHtmlScope<AppRoot>
{
    private readonly IHtmlTestContext _context;

    /// <summary>The root scope of <paramref name="context"/>.</summary>
    public AppRoot(IHtmlTestContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public override IHtmlTestContext Context => _context;

    /// <inheritdoc />
    public AppRoot Self => this;

    /// <summary>None: this scope is not a page and is not in one.</summary>
    public IHtmlPage? Page => null;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <inheritdoc />
    public ScopeReadiness ProbeReadiness() => _context.ProbeReadiness() with { ScopeName = nameof(AppRoot) };

    /// <inheritdoc />
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    /// <summary>Waits for the page to be usable, as one call with its own budget (MAUI review-2, fix 8).</summary>
    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? DefaultTimeoutMs);

    /// <inheritdoc />
    public IHtmlElement? TryFindElement(Locator locator) => _context.TryFindElement(locator);

    /// <inheritdoc />
    public IHtmlElement FindElement(Locator locator) => _context.FindElement(locator);

    /// <inheritdoc />
    public IReadOnlyList<IHtmlElement> FindElements(Locator locator) => _context.FindElements(locator);
}
