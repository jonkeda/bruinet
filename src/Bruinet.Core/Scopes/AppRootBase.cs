using Bruinet.Core.Calls;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;

namespace Bruinet.Core.Scopes;

/// <summary>
/// The whole screen as a scope, for controls that do not live inside a page object: the system's
/// permission dialog, a toast, a snackbar (public API review, proposal 5). Each stack's sealed
/// <c>AppRoot</c> derives from this with its context and element types.
/// </summary>
/// <typeparam name="TContext">The stack's test context, which is also its root element scope.</typeparam>
/// <typeparam name="TElement">The stack's element.</typeparam>
public abstract class AppRootBase<TContext, TElement> : ObjectBase<TContext>
    where TContext : ITestContext, IElementScope<TElement>
    where TElement : class
{
    private readonly TContext _context;

    /// <summary>The root scope of <paramref name="context"/>.</summary>
    protected AppRootBase(TContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public sealed override TContext Context => _context;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <inheritdoc />
    public ScopeReadiness ProbeReadiness() => _context.ProbeReadiness() with { ScopeName = "AppRoot" };

    /// <inheritdoc />
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    /// <summary>Waits for the app to be in front, as one call with its own budget.</summary>
    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? DefaultTimeoutMs);

    /// <inheritdoc />
    public TElement? TryFindElement(Locator locator) => _context.TryFindElement(locator);

    /// <inheritdoc />
    public TElement FindElement(Locator locator) => _context.FindElement(locator);

    /// <inheritdoc />
    public IReadOnlyList<TElement> FindElements(Locator locator) => _context.FindElements(locator);
}
