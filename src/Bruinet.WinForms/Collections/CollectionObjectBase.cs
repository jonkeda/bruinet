using Bruinet.Core.Calls;
using Bruinet.Core.Scopes;
using Bruinet.WinForms.Containers;

namespace Bruinet.WinForms.Collections;

/// <summary>
/// A list of item rows (a <c>ListBox</c>, a <c>ListView</c>, a <c>DataGridView</c>) that hands out typed
/// row objects (design 7). Rows are identified by <see cref="ItemKey"/>, and an item that is not
/// shown is found by scrolling the list a page, one page per attempt within the call's budget: a
/// list in virtual mode leaves the rows it has not shown out of the tree.
/// </summary>
/// <typeparam name="TParent">The scope the collection is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TParent, TSelf, TItem> : ContainerObjectBase<TParent, TSelf>, IWinFormsCollection
    where TParent : IWinFormsScope<TParent>
    where TSelf : CollectionObjectBase<TParent, TSelf, TItem>
    where TItem : ItemObjectBase<TItem>
{
    /// <summary>A list's rows: its list items.</summary>
    protected static readonly Locator ListItems = Locator.ByControlType("ListItem");

    /// <summary>A data grid's rows.</summary>
    protected static readonly Locator DataItems = Locator.ByControlType("DataItem");

    private readonly Locator _itemLocator;

    /// <summary>The list found by <paramref name="locator"/>, whose rows match <paramref name="itemLocator"/> inside it (its list items by default).</summary>
    protected CollectionObjectBase(IWinFormsScope<TParent> parent, Locator locator, Locator? itemLocator = null)
        : base(parent, locator)
    {
        _itemLocator = itemLocator ?? ListItems;
    }

    /// <summary>The list with AutomationId <paramref name="automationId"/>.</summary>
    protected CollectionObjectBase(IWinFormsScope<TParent> parent, string automationId, Locator? itemLocator = null)
        : this(parent, Locator.ByAutomationId(automationId), itemLocator)
    {
    }

    /// <summary>The element inside a row whose text is the row's key; null to use the row's first text or name.</summary>
    protected virtual Locator? ItemKeyLocator => null;

    /// <summary>Whether looking for an item that is not shown scrolls the list. A short, fixed list says no.</summary>
    protected virtual bool ScrollsToFind => true;

    /// <summary>Makes the row object for <paramref name="key"/>, found at <paramref name="position"/>.</summary>
    protected abstract TItem CreateItem(ItemKey key, int position);

    /// <summary>The key of a row element.</summary>
    protected ItemKey KeyOf(IWinFormsElement row, int position) => ItemKeys.Of(row, position, ItemKeyLocator);

    /// <inheritdoc />
    /// <remarks>
    /// A keyed row is found by its key, not by where it is: a list in virtual mode gives the row
    /// at a position to another item as it scrolls, so a row found by position would answer for
    /// whichever item is there now.
    /// </remarks>
    public IWinFormsElement? TryGetItemRoot(ItemKey key)
    {
        var rows = TryGetItemRoots();
        if (key.Kind == ItemKeyKind.Position)
        {
            return rows.ElementAtOrDefault(int.Parse(key.Value, CultureInfo.InvariantCulture));
        }

        for (var position = 0; position < rows.Count; position++)
        {
            if (KeyOf(rows[position], position) == key)
            {
                return rows[position];
            }
        }

        return null;
    }

    /// <summary>The rows in the tree now. One attempt.</summary>
    protected IReadOnlyList<IWinFormsElement> TryGetItemRoots() => FindElements(_itemLocator);

    /// <summary>How many rows are in the tree now.</summary>
    public int GetItemCount() => TryGetItemRoots().Count;

    /// <summary>The rows in the tree now, as row objects. One attempt.</summary>
    public IReadOnlyList<TItem> Items => [.. TryGetItemRoots().Select((row, position) => CreateItem(KeyOf(row, position), position))];

    /// <summary>The row at <paramref name="index"/> among those in the tree now, or <c>null</c>.</summary>
    public TItem? TryItem(int index)
    {
        var rows = TryGetItemRoots();
        return index >= 0 && index < rows.Count ? CreateItem(KeyOf(rows[index], index), index) : null;
    }

    /// <summary>The row whose key is <paramref name="key"/> among those in the tree now, or <c>null</c>.</summary>
    public TItem? TryItem(string key)
    {
        var rows = TryGetItemRoots();
        for (var position = 0; position < rows.Count; position++)
        {
            if (KeyOf(rows[position], position) is { Kind: ItemKeyKind.Logical } found && found.Value == key)
            {
                return CreateItem(found, position);
            }
        }

        return null;
    }

    /// <summary>The row at <paramref name="index"/> among those in the tree, waiting for it. Does not scroll: a position is only meaningful on the rows realized.</summary>
    /// <exception cref="ElementNotFoundException">There was no such row within the budget.</exception>
    public TItem Item(int index, int? timeoutMs = null)
        => WaitFor(() => TryItem(index), $"row {index}", scroll: false, timeoutMs);

    /// <summary>The row of item <paramref name="key"/>, scrolling the list to it when it is not in the tree.</summary>
    /// <exception cref="ElementNotFoundException">There was no such item within the budget.</exception>
    public TItem Item(string key, int? timeoutMs = null)
        => WaitFor(() => TryItem(key), $"item '{key}'", ScrollsToFind, timeoutMs);

    /// <summary>The first row that matches <paramref name="predicate"/>, scrolling the list to find one.</summary>
    /// <exception cref="ElementNotFoundException">No row matched within the budget.</exception>
    public TItem ItemWhere(Func<TItem, bool> predicate, int? timeoutMs = null)
        => WaitFor(() => Items.FirstOrDefault(predicate), "a row that matches", ScrollsToFind, timeoutMs);

    /// <summary>
    /// Waits until no row in the tree matches <paramref name="predicate"/>. A list that is not shown
    /// at all has no rows, so its missing root is an answer, not a wait.
    /// </summary>
    public bool WaitNoItemWhere(Func<TItem, bool> predicate, int? timeoutMs = null)
        => RunProbe(() => ProbeReadiness() switch
            {
                { IsReady: true } => Items.Any(predicate) ? Observation.Pending("a row still matches") : Observation.Done(),
                { State: ScopeReadinessState.MissingRoot } own when own.ScopeName == ScopeName => Observation.Done(),
                var notReady => ScopeGate.Check(notReady) ?? Observation.Done(),
            },
            timeoutMs ?? DefaultTimeoutMs);

    /// <summary>Waits until <paramref name="expected"/> rows are in the tree.</summary>
    public bool WaitItemCount(int expected, int? timeoutMs = null)
        => RunProbe(() => ScopeGate.Check(ProbeReadiness())
            ?? (GetItemCount() == expected ? Observation.Done() : Observation.Pending($"{GetItemCount()} rows")),
            timeoutMs ?? DefaultTimeoutMs);

    /// <summary>Fails unless <paramref name="expected"/> rows are in the tree within the budget.</summary>
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

    // One call: each attempt looks once; a miss scrolls the list one page, onwards, then back the
    // other way when the end is reached, and stops scrolling when both ends were seen.
    private TItem WaitFor(Func<TItem?> find, string what, bool scroll, int? timeoutMs)
    {
        TItem? found = null;
        var budget = timeoutMs ?? DefaultTimeoutMs;
        var down = true;
        var endsSeen = 0;
        RunProbe(
            () =>
            {
                if (ScopeGate.Check(ProbeReadiness()) is { } notReady)
                {
                    return notReady;
                }

                if ((found = find()) != null)
                {
                    return Observation.Done();
                }

                if (scroll && endsSeen < 2 && TryFindRootElement() is { } list && !list.ScrollPage(down))
                {
                    endsSeen++;
                    down = !down;
                }

                return Observation.Missing(what);
            },
            budget,
            last => new ElementNotFoundException($"{ScopeName} had no {what} within {budget} ms. Last seen: {last}."));
        return found!;
    }
}
