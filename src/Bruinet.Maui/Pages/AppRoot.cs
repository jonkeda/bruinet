using Bruinet.Core.Calls;
using Bruinet.Core.Interfaces;

namespace Bruinet.Maui.Pages;

/// <summary>
/// The whole app as a scope, for the controls that do not live inside a page.
/// </summary>
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

    /// <inheritdoc cref="IMauiElementScope.IsReady"/>
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null)
    {
        var budget = timeoutMs ?? DefaultTimeoutMs;
        var context = new AttemptContext(Deadline.In(budget), Context.Timeouts.Animation);

        return Poller.Until(
            _ => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(),
            context,
            PollingIntervalMs);
    }

    /// <summary>
    /// Waits, as one call, until one of <paramref name="outcomes"/> holds, and returns its index;
    /// the earlier outcome wins when several hold.
    /// </summary>
    /// <param name="timeoutMs">The whole budget; the default wait when null.</param>
    /// <param name="outcomes">Answers about now, such as <c>page.IsReady</c> or <c>control.IsExists</c>; none may wait itself.</param>
    /// <returns>The index of the outcome that held, or -1 when none did within the budget.</returns>
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        if (outcomes.Length == 0)
            throw new ArgumentException("Give at least one outcome.", nameof(outcomes));

        var budget = timeoutMs ?? DefaultTimeoutMs;
        var call = new ControlCall(Context.Logger, Context.Name, Context.NearMiss, nameof(AppRoot), nameof(AppRoot));
        return call.Run(nameof(WaitFirst), $"{outcomes.Length} outcomes", budget, Context.Timeouts.Animation, context =>
        {
            var held = -1;
            var met = Poller.Until(_ =>
            {
                if (ScopeGate.Check(ProbeReadiness()) is { } notReady)
                    return notReady;

                held = Array.FindIndex(outcomes, outcome => outcome());
                return held >= 0 ? Observation.Done() : Observation.Pending("no outcome held");
            }, context, PollingIntervalMs);

            if (!met && context.Log.Last is { Kind: ObservationKind.Failed, Error: { } error })
            {
                var failure = context.Log.Unexpected(error, nameof(WaitFirst), budget);
                if (ReferenceEquals(failure, error))
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                throw failure;
            }

            return met ? held : -1;
        }, succeeded: index => index >= 0);
    }

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
