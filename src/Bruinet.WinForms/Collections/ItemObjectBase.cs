using Bruinet.Core.Calls;
using Bruinet.Core.Scopes;

namespace Bruinet.WinForms.Collections;

/// <summary>
/// One row of a collection, as a scope for the controls inside it. The row stands for one item
/// (<see cref="Key"/>) and finds its element again by that key on every lookup (design 7). When its
/// item is gone, a call on the row waits for it, then fails naming it; it never finds another
/// item's row in its place (R0).
/// </summary>
/// <typeparam name="TSelf">The row's own type.</typeparam>
public abstract class ItemObjectBase<TSelf> : ObjectBase, IWinFormsScope<TSelf>
    where TSelf : ItemObjectBase<TSelf>
{
    private readonly IWinFormsCollection _collection;

    /// <summary>The row of <paramref name="key"/>, found at <paramref name="position"/> when it was made.</summary>
    protected ItemObjectBase(IWinFormsCollection collection, ItemKey key, int position)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        Key = key;
        Position = position;
    }

    /// <summary>The item this row stands for.</summary>
    public ItemKey Key { get; }

    /// <summary>Where the row was among the rows shown when it was made. It may have moved since.</summary>
    public int Position { get; }

    /// <inheritdoc />
    public override IWinFormsTestContext Context => _collection.Context;

    /// <inheritdoc />
    public abstract TSelf Self { get; }

    /// <inheritdoc />
    public IWinFormsPage? Page => _collection.Page;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _collection.DefaultLocatorStrategy;

    /// <inheritdoc />
    protected override string ScopeName => $"{GetType().Name}({Key})";

    /// <summary>The collection first, then the row of this item (R4).</summary>
    public ScopeReadiness ProbeReadiness()
    {
        if (_collection.ProbeReadiness() is { IsReady: false } collection)
        {
            return collection;
        }

        return _collection.TryGetItemRoot(Key) is null
            ? new ScopeReadiness(ScopeName, ScopeReadinessState.ItemChanged, $"{Key} is no longer shown in the list")
            : ScopeReadiness.Ready(ScopeName);
    }

    /// <inheritdoc />
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? DefaultTimeoutMs);

    /// <summary>Whether the row's item is still shown in the list now.</summary>
    public bool Exists() => _collection.TryGetItemRoot(Key) != null;

    /// <inheritdoc />
    public IWinFormsElement? TryFindElement(Locator locator) => _collection.TryGetItemRoot(Key)?.TryFindElement(locator);

    /// <inheritdoc />
    public IWinFormsElement FindElement(Locator locator) => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    /// <inheritdoc />
    public IReadOnlyList<IWinFormsElement> FindElements(Locator locator)
        => _collection.TryGetItemRoot(Key)?.FindElements(locator) ?? [];
}
