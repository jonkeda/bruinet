using Bruinet.Core.Collections;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Collections;

/// <summary>
/// One row of a collection, as a scope for the controls inside it. The row stands for one item
/// (<see cref="Key"/>) and finds its element again by that key on every lookup (design 7.6).
/// When its item is gone, a call on the row waits for it, then fails naming it; it never
/// finds another item's row in its place (call model R0).
/// </summary>
public abstract class ItemObjectBase<TSelf> : ObjectBase, IHtmlScope<TSelf>
    where TSelf : ItemObjectBase<TSelf>
{
    private readonly IHtmlCollection _collection;

    /// <summary>The row of <paramref name="key"/>, found at <paramref name="position"/> when it was made.</summary>
    protected ItemObjectBase(IHtmlCollection collection, ItemKey key, int position)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        Key = key;
        Position = position;
    }

    /// <summary>The item this row stands for.</summary>
    public ItemKey Key { get; }

    /// <summary>Where the row was when it was made, or <c>-1</c> for a row found by its key. It may have moved since.</summary>
    public int Position { get; }

    public override IHtmlTestContext Context => _collection.Context;

    public abstract TSelf Self { get; }

    public IHtmlPage? Page => _collection.Page;

    public LocatorStrategy DefaultLocatorStrategy => _collection.DefaultLocatorStrategy;

    protected override string ScopeName => $"{GetType().Name}({Key})";

    /// <summary>The collection first, then the row of this item (R4).</summary>
    public ScopeReadiness ProbeReadiness()
    {
        if (_collection.ProbeReadiness() is { IsReady: false } collection)
        {
            return collection;
        }

        return _collection.TryGetItemRoot(Key) is null
            ? new ScopeReadiness(ScopeName, ScopeReadinessState.ItemChanged, $"{Key} is no longer in the list")
            : ScopeReadiness.Ready(ScopeName);
    }

    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? DefaultTimeoutMs);

    /// <summary>Whether the row's item is still in the list now.</summary>
    public bool Exists() => _collection.TryGetItemRoot(Key) != null;

    public IHtmlElement? TryFindElement(Locator locator) => _collection.TryGetItemRoot(Key)?.TryFindElement(locator);

    public IHtmlElement FindElement(Locator locator) => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    public IReadOnlyList<IHtmlElement> FindElements(Locator locator)
        => _collection.TryGetItemRoot(Key)?.FindElements(locator) ?? [];
}
