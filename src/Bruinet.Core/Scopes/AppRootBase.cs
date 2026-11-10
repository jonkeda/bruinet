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

    /// <summary>How a bare string locator inside this scope is read.</summary>
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <summary>Whether this scope is ready now, and if not, why. One look.</summary>
    public ScopeReadiness ProbeReadiness() => _context.ProbeReadiness() with { ScopeName = "AppRoot" };

    /// <summary>Whether this scope is ready now. One look.</summary>
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <summary>Waits for the first of <paramref name="outcomes"/> that holds and answers its index, or -1 when none held within the budget.</summary>
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    /// <summary>Waits for the app to be in front, as one call with its own budget.</summary>
    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? DefaultTimeoutMs);

    /// <summary>The element <paramref name="locator"/> finds in this scope now, or null. One attempt.</summary>
    public TElement? TryFindElement(Locator locator) => _context.TryFindElement(locator);

    /// <summary>The element <paramref name="locator"/> finds in this scope now; throws when there is none.</summary>
    public TElement FindElement(Locator locator) => _context.FindElement(locator);

    /// <summary>Every element <paramref name="locator"/> finds in this scope now. One attempt.</summary>
    public IReadOnlyList<TElement> FindElements(Locator locator) => _context.FindElements(locator);
}
