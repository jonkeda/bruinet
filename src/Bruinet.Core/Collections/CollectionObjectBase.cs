using System.Globalization;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;

namespace Bruinet.Core.Collections;

/// <summary>
/// A list of item rows that hands out typed row objects (public API review, proposal 5). Rows are
/// identified by <see cref="ItemKey"/>; an item that is not shown is found by scrolling the list one
/// step per attempt within the call's budget, on a stack whose lists scroll. Each stack's
/// <c>CollectionObjectBase&lt;TParent, TSelf, TItem&gt;</c> derives from this and says where its rows
/// are, how a row's key is read, and how its list scrolls.
/// </summary>
/// <typeparam name="TContext">The stack's test context.</typeparam>
/// <typeparam name="TElement">The stack's element.</typeparam>
/// <typeparam name="TParentScope">The stack's scope the list is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TContext, TElement, TParentScope, TSelf, TItem>
    : ContainerObjectBase<TContext, TElement, TParentScope, TSelf>, IItemRootSource<TElement>
    where TContext : ITestContext, IScope
    where TElement : class
    where TParentScope : IElementScope<TElement>
    where TItem : class
{
    /// <summary>The list found by <paramref name="locator"/> in <paramref name="parentScope"/>.</summary>
    protected CollectionObjectBase(TParentScope parentScope, Locator locator)
        : base(parentScope, locator)
    {
    }

    /// <summary>Makes the row object for <paramref name="key"/>, found at <paramref name="position"/>.</summary>
    protected abstract TItem CreateItem(ItemKey key, int position);

    /// <summary>The key of a row element.</summary>
    protected abstract ItemKey KeyOf(TElement row, int position);

    /// <summary>The rows in the tree now. One attempt.</summary>
    protected abstract IReadOnlyList<TElement> TryGetItemRoots();

    /// <summary>Whether looking for an item that is not shown scrolls the list. A short, fixed list says no.</summary>
    protected virtual bool ScrollsToFind => false;

    /// <summary>Scrolls <paramref name="list"/> one step, onwards or back; false when it was already at that end.</summary>
    protected virtual bool ScrollStep(TElement list, bool onwards) => false;

    /// <inheritdoc />
    /// <remarks>
    /// A keyed row is found by its key, not by where it is: a virtualizing list gives the row at a
    /// position to another item as it scrolls, so a row found by position would answer for whichever
    /// item is there now.
    /// </remarks>
    public virtual TElement? TryGetItemRoot(ItemKey key)
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
    public virtual TItem? TryItem(string key)
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
    public virtual TItem Item(string key, int? timeoutMs = null)
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
                return new ControlAssertionException(message ?? $"Expected {expected} rows in {ScopeName}, but there are {actual}.", expected, actual);
            });
        return Self;
    }

    /// <summary>
    /// One call that looks for a row: each attempt looks once; on a list that scrolls, a miss scrolls
    /// it one step, onwards, then back the other way when the end is reached, and stops scrolling when
    /// both ends were seen.
    /// </summary>
    /// <exception cref="ElementNotFoundException">Nothing was found within the budget.</exception>
    protected TItem WaitFor(Func<TItem?> find, string what, bool scroll, int? timeoutMs)
    {
        ArgumentNullException.ThrowIfNull(find);
        TItem? found = null;
        var budget = timeoutMs ?? DefaultTimeoutMs;
        var onwards = true;
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

                if (scroll && endsSeen < 2 && TryFindRootElement() is { } list && !ScrollStep(list, onwards))
                {
                    endsSeen++;
                    onwards = !onwards;
                }

                return Observation.Missing(what);
            },
            budget,
            last => new ElementNotFoundException($"{ScopeName} had no {what} within {budget} ms. Last seen: {last}."));
        return found!;
    }
}
