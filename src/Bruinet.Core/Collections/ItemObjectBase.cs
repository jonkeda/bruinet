using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;

namespace Bruinet.Core.Collections;

/// <summary>
/// One row of a collection, as a scope for the controls inside it (public API review, proposal 5).
/// The row stands for one item (<see cref="Key"/>) and finds its element again by that key on every
/// lookup. When its item is gone, a call on the row waits for it, then fails naming it; it never
/// finds another item's row in its place (R0). Each stack's <c>ItemObjectBase&lt;TSelf&gt;</c> derives
/// from this with its types and says how to search inside a row's element.
/// </summary>
/// <typeparam name="TContext">The stack's test context.</typeparam>
/// <typeparam name="TElement">The stack's element.</typeparam>
/// <typeparam name="TCollection">The collection the row belongs to.</typeparam>
/// <typeparam name="TSelf">The row's own type.</typeparam>
public abstract class ItemObjectBase<TContext, TElement, TCollection, TSelf> : ObjectBase<TContext>
    where TContext : ITestContext, IScope
    where TElement : class
    where TCollection : IItemRootSource<TElement>, IScope
    where TSelf : ItemObjectBase<TContext, TElement, TCollection, TSelf>
{
    /// <summary>The row of <paramref name="key"/>, found at <paramref name="position"/> when it was made.</summary>
    protected ItemObjectBase(TCollection collection, ItemKey key, int position)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        Key = key;
        Position = position;
    }

    /// <summary>The collection the row belongs to.</summary>
    protected TCollection Collection { get; }

    /// <summary>The item this row stands for.</summary>
    public ItemKey Key { get; }

    /// <summary>
    /// Where the row was among the rows shown when it was made; it may have moved since. <c>-1</c>
    /// when the row was found by its key without counting the rows.
    /// </summary>
    public int Position { get; }

    /// <summary>This object, as its own type, for fluent chains.</summary>
    public abstract TSelf Self { get; }

    /// <summary>How a bare string locator inside this scope is read.</summary>
    public LocatorStrategy DefaultLocatorStrategy => Collection.DefaultLocatorStrategy;

    /// <inheritdoc />
    protected override string ScopeName => $"{GetType().Name}({Key})";

    /// <summary>The collection first, then the row of this item (R4).</summary>
    public ScopeReadiness ProbeReadiness()
    {
        if (Collection.ProbeReadiness() is { IsReady: false } collection)
        {
            return collection;
        }

        return Collection.TryGetItemRoot(Key) is null
            ? new ScopeReadiness(ScopeName, ScopeReadinessState.ItemChanged, $"{Key} is no longer shown in the list")
            : ScopeReadiness.Ready(ScopeName);
    }

    /// <summary>Whether this scope is ready now. One look.</summary>
    public bool IsReady() => ProbeReadiness().IsReady;

    /// <summary>Waits for the first of <paramref name="outcomes"/> that holds and answers its index, or -1 when none held within the budget.</summary>
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => RunFirst(timeoutMs, outcomes);

    /// <summary>Waits until this scope is ready, as one call with its own budget.</summary>
    public bool WaitReady(int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness()) ?? Observation.Done(), timeoutMs ?? DefaultTimeoutMs);

    /// <summary>Whether the row's item is still shown in the list now.</summary>
    public bool Exists() => Collection.TryGetItemRoot(Key) != null;

    /// <summary>The element <paramref name="locator"/> finds in this scope now, or null. One attempt.</summary>
    public TElement? TryFindElement(Locator locator)
        => Collection.TryGetItemRoot(Key) is { } root ? TryFindIn(root, locator) : null;

    /// <summary>The element <paramref name="locator"/> finds in this scope now; throws when there is none.</summary>
    public TElement FindElement(Locator locator) => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    /// <summary>Every element <paramref name="locator"/> finds in this scope now. One attempt.</summary>
    public IReadOnlyList<TElement> FindElements(Locator locator)
        => Collection.TryGetItemRoot(Key) is { } root ? FindAllIn(root, locator) : [];

    /// <summary>The element <paramref name="locator"/> finds inside <paramref name="root"/>, or null.</summary>
    protected abstract TElement? TryFindIn(TElement root, Locator locator);

    /// <summary>Every element <paramref name="locator"/> finds inside <paramref name="root"/>.</summary>
    protected abstract IReadOnlyList<TElement> FindAllIn(TElement root, Locator locator);
}
