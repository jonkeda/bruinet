using Bruinet.Core.Calls;
using Bruinet.Core.Scopes;

namespace Bruinet.WinForms.Pages;

/// <summary>
/// The whole screen as a scope, for controls that do not live inside a page object: the system's
/// permission dialog, a toast, a snackbar.
/// </summary>
public sealed class AppRoot : ObjectBase, IWinFormsScope<AppRoot>
{
    private readonly IWinFormsTestContext _context;

    /// <summary>The root scope of <paramref name="context"/>.</summary>
    public AppRoot(IWinFormsTestContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public override IWinFormsTestContext Context => _context;

    /// <inheritdoc />
    public AppRoot Self => this;

    /// <summary>None: this scope is not a page and is not in one.</summary>
    public IWinFormsPage? Page => null;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <inheritdoc />
    public ScopeReadiness ProbeReadiness() => _context.ProbeReadiness() with { ScopeName = nameof(AppRoot) };

    /// <inheritdoc />
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    /// <summary>Waits for the app to be in front, as one call with its own budget.</summary>
    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? DefaultTimeoutMs);

    /// <inheritdoc />
    public IWinFormsElement? TryFindElement(Locator locator) => _context.TryFindElement(locator);

    /// <inheritdoc />
    public IWinFormsElement FindElement(Locator locator) => _context.FindElement(locator);

    /// <inheritdoc />
    public IReadOnlyList<IWinFormsElement> FindElements(Locator locator) => _context.FindElements(locator);
}
