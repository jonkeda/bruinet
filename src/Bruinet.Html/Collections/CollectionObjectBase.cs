using Bruinet.Core.Collections;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;
using Bruinet.Html.Controls;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Collections;

/// <summary>
/// A container of item rows that hands out typed row objects (design 7.6).
/// Rows are identified by <see cref="ItemKey"/>: their <c>data-item-key</c>, or their position.
/// </summary>
/// <typeparam name="TParent">The scope the collection is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TParent, TSelf, TItem> : ContainerBase<TParent, TSelf>, IHtmlCollection
    where TParent : IHtmlScope<TParent>
    where TSelf : CollectionObjectBase<TParent, TSelf, TItem>
    where TItem : ItemObjectBase<TItem>
{
    private readonly Locator _itemLocator;

    /// <summary>The collection found by <paramref name="locator"/>, whose rows match <paramref name="itemLocator"/> inside it.</summary>
    protected CollectionObjectBase(IHtmlScope<TParent> parent, Locator locator, Locator itemLocator)
        : base(parent, locator)
    {
        _itemLocator = itemLocator ?? throw new ArgumentNullException(nameof(itemLocator));
    }

    /// <summary>Makes the row object for <paramref name="key"/>, found at <paramref name="position"/>.</summary>
    protected abstract TItem CreateItem(ItemKey key, int position);

    /// <inheritdoc />
    /// <remarks>
    /// A keyed row is found by its key, not by where it is: an element found by position is found
    /// again by position on every read, so after a re-render it would read whichever row is now
    /// there, and a row object would answer for another item.
    /// </remarks>
    public IHtmlElement? TryGetItemRoot(ItemKey key)
        => key.Kind == ItemKeyKind.Logical
            ? TryFindRootElement()?.TryFindElement(KeySelector(key.Value))
            : TryGetItemRoots().ElementAtOrDefault(int.Parse(key.Value, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>The row whose <c>data-item-key</c> is <paramref name="key"/>, as a CSS selector.</summary>
    private static Locator KeySelector(string key)
        => Locator.ByCss($"[{ItemKeys.Attribute}=\"{key.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"]");

    /// <summary>The rows now. One attempt.</summary>
    protected IReadOnlyList<IHtmlElement> TryGetItemRoots() => FindElements(_itemLocator);

    /// <summary>How many rows there are now.</summary>
    public int GetItemCount() => TryGetItemRoots().Count;

    /// <summary>The rows now, as row objects. One attempt.</summary>
    public IReadOnlyList<TItem> Items => [.. TryGetItemRoots().Select((row, position) => CreateItem(ItemKeys.Of(row, position), position))];

    /// <summary>The row at <paramref name="index"/> now, or <c>null</c>.</summary>
    public TItem? TryItem(int index)
    {
        var rows = TryGetItemRoots();
        return index >= 0 && index < rows.Count ? CreateItem(ItemKeys.Of(rows[index], index), index) : null;
    }

    /// <summary>The row whose <c>data-item-key</c> is <paramref name="key"/> now, or <c>null</c>.</summary>
    /// <remarks>
    /// One lookup by the key's selector, as every later read of the row is: reading each row's key
    /// in turn costs a driver call per row, and a long list then outlasts the call's budget.
    /// The row's <see cref="ItemObjectBase{TSelf}.Position"/> is not looked up, so it is <c>-1</c>.
    /// </remarks>
    public TItem? TryItem(string key)
    {
        var itemKey = new ItemKey(ItemKeyKind.Logical, key);
        return TryGetItemRoot(itemKey) is null ? null : CreateItem(itemKey, -1);
    }

    /// <summary>The row at <paramref name="index"/>, waiting for it.</summary>
    /// <exception cref="ElementNotFoundException">There was no such row within the budget.</exception>
    public TItem Item(int index, int? timeoutMs = null)
        => WaitFor(() => TryItem(index), $"row {index}", timeoutMs);

    /// <summary>The row of item <paramref name="key"/>, waiting for it.</summary>
    /// <exception cref="ElementNotFoundException">There was no such item within the budget.</exception>
    public TItem Item(string key, int? timeoutMs = null)
        => WaitFor(() => TryItem(key), $"item '{key}'", timeoutMs);

    /// <summary>The first row that matches <paramref name="predicate"/>, waiting for one.</summary>
    /// <exception cref="ElementNotFoundException">No row matched within the budget.</exception>
    public TItem ItemWhere(Func<TItem, bool> predicate, int? timeoutMs = null)
        => WaitFor(() => Items.FirstOrDefault(predicate), "a row that matches", timeoutMs);

    /// <summary>
    /// Waits until no row matches <paramref name="predicate"/>. A collection that is not shown at all
    /// has no rows, so its missing root is an answer, not a wait.
    /// </summary>
    public bool WaitNoItemWhere(Func<TItem, bool> predicate, int? timeoutMs = null)
        => RunProbe(() => ProbeReadiness() switch
            {
                { IsReady: true } => Items.Any(predicate) ? Observation.Pending("a row still matches") : Observation.Done(),
                { State: ScopeReadinessState.MissingRoot } own when own.ScopeName == ScopeName => Observation.Done(),
                var notReady => ScopeGate.Check(notReady) ?? Observation.Done(),
            },
            timeoutMs ?? DefaultTimeoutMs);

    /// <summary>Waits until there are <paramref name="expected"/> rows.</summary>
    public bool WaitItemCount(int expected, int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness())
            ?? (GetItemCount() == expected ? Observation.Done() : Observation.Pending($"{GetItemCount()} rows")),
            timeoutMs ?? DefaultTimeoutMs);

    /// <summary>Fails unless there are <paramref name="expected"/> rows within the budget.</summary>
    public TSelf AssertItemCount(int expected, string? message = null, int? timeoutMs = null)
    {
        RunProbe(
            () => ScopeGate.Check(ProbeReadiness())
                ?? (GetItemCount() == expected ? Observation.Done() : Observation.Pending($"{GetItemCount()} rows")),
            timeoutMs ?? DefaultTimeoutMs,
            _ =>
            {
                var actual = GetItemCount();
                return new AssertionException(message ?? $"Expected {expected} rows in {ScopeName}, but there are {actual}.", expected, actual);
            });
        return Self;
    }

    private TItem WaitFor(Func<TItem?> find, string what, int? timeoutMs)
    {
        TItem? found = null;
        var budget = timeoutMs ?? DefaultTimeoutMs;
        RunProbe(
            () => ScopeGate.Check(ProbeReadiness()) ?? ((found = find()) != null ? Observation.Done() : Observation.Missing(what)),
            budget,
            last => new ElementNotFoundException($"{ScopeName} had no {what} within {budget} ms. Last seen: {last}."));
        return found!;
    }
}
