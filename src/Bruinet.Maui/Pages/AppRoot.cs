using Bruinet.Core.Configuration;
using Bruinet.Core.Calls;
using Bruinet.Core.Interfaces;

namespace Bruinet.Maui.Pages;

/// <summary>
/// The whole app as a scope, for the controls that do not live inside a page.
/// </summary>
/// <remarks>
/// MAUI keeps its own app root rather than Core's <c>AppRootBase</c> (public API review, proposal 5,
/// rule 6): it waits without a logged call and answers whether scroll lookups are allowed.
/// </remarks>
public sealed class AppRoot : ObjectBase, IMauiScope<AppRoot>
{
    private readonly IMauiTestContext _context;

    /// <summary>Creates the root scope for a session.</summary>
    /// <param name="context">The test context, which is the driver root.</param>
    public AppRoot(IMauiTestContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public override IMauiTestContext Context => _context;

    /// <inheritdoc />
    public AppRoot Self => this;

    /// <summary>
    /// None: this scope is not a page and is not in one.
    /// </summary>
    public IMauiPage? Page => null;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <inheritdoc />
    public ScopeReadiness ProbeReadiness() => _context.ProbeReadiness() with { ScopeName = nameof(AppRoot) };

    /// <inheritdoc cref="IScope.IsReady"/>
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null)
    {
        var budget = timeoutMs ?? DefaultTimeoutMs;
        var context = new AttemptContext(Deadline.In(budget), Context.Timeouts.Animation.ToMs());

        return Poller.Until(
            _ => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(),
            context,
            PollingIntervalMs);
    }

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes)
        => FirstOutcome.Wait(
            new ControlCall(Context.Logger, Context.Name, Context.NearMiss, nameof(AppRoot), nameof(AppRoot)),
            Context.ProbeReadiness, outcomes, timeoutMs ?? DefaultTimeoutMs, Context.Timeouts.Animation.ToMs(), PollingIntervalMs);

    /// <inheritdoc />
    public bool AllowsScrollLookup => false;

    /// <inheritdoc />
    public IMauiElement? TryFindElement(Locator locator) => _context.TryFindElement(locator);

    /// <inheritdoc />
    public IMauiElement FindElement(Locator locator) => _context.FindElement(locator);

    /// <inheritdoc />
    public ElementNotFoundException DescribeMiss(Locator locator) => _context.DescribeMiss(locator);

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindElements(Locator locator)
        => _context.FindElements(locator);
}
