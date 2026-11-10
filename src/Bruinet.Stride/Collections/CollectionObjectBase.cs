using Bruinet.Core.Calls;
using Bruinet.Core.Scopes;
using Bruinet.Stride.Containers;

namespace Bruinet.Stride.Collections;

/// <summary>
/// A list of item rows (a <c>StackPanel</c> or <c>UniformGrid</c> of rows) that hands out typed row
/// objects (design 7). Its rows are the panel's children, identified by <see cref="ItemKey"/>. Stride's
/// UI keeps every row in place, so nothing scrolls to find one.
/// </summary>
/// <typeparam name="TParent">The scope the collection is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TParent, TSelf, TItem> : ContainerObjectBase<TParent, TSelf>, IStrideCollection
    where TParent : IStrideScope<TParent>
    where TSelf : CollectionObjectBase<TParent, TSelf, TItem>
    where TItem : ItemObjectBase<TItem>
{
    /// <summary>The list found by <paramref name="locator"/>; its rows are its children.</summary>
    protected CollectionObjectBase(IStrideScope<TParent> parent, Locator locator)
        : base(parent, locator)
    {
    }

    /// <summary>The list named <paramref name="automationId"/>.</summary>
    protected CollectionObjectBase(IStrideScope<TParent> parent, string automationId)
        : this(parent, Locator.ByAutomationId(automationId))
    {
    }

    /// <summary>The element inside a row whose text is the row's key; null to use the row's own text, first child's text or name.</summary>
    protected virtual Locator? ItemKeyLocator => null;

    /// <summary>Makes the row object for <paramref name="key"/>, found at <paramref name="position"/>.</summary>
    protected abstract TItem CreateItem(ItemKey key, int position);

    /// <summary>The key of a row element.</summary>
    protected ItemKey KeyOf(IStrideElement row, int position) => ItemKeys.Of(row, position, ItemKeyLocator);

    /// <inheritdoc />
    /// <remarks>
    /// A keyed row is found by its key, not by where it is: a list the game rebuilds puts another item
    /// at a position, so a row found by position would answer for whichever item is there now.
    /// </remarks>
    public IStrideElement? TryGetItemRoot(ItemKey key)
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

    /// <summary>The rows now: the list's children. One attempt.</summary>
    protected IReadOnlyList<IStrideElement> TryGetItemRoots() => TryFindRootElement()?.Children() ?? [];

    /// <summary>How many rows there are now.</summary>
    public int GetItemCount() => TryGetItemRoots().Count;

    /// <summary>The rows now, as row objects. One attempt.</summary>
    public IReadOnlyList<TItem> Items => [.. TryGetItemRoots().Select((row, position) => CreateItem(KeyOf(row, position), position))];

    /// <summary>The row at <paramref name="index"/> now, or <c>null</c>.</summary>
    public TItem? TryItem(int index)
    {
        var rows = TryGetItemRoots();
        return index >= 0 && index < rows.Count ? CreateItem(KeyOf(rows[index], index), index) : null;
    }

    /// <summary>The row whose key is <paramref name="key"/> now, or <c>null</c>.</summary>
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

    /// <summary>The row at <paramref name="index"/>, waiting for it, as one call.</summary>
    /// <exception cref="ElementNotFoundException">There was no such row within the budget.</exception>
    public TItem Item(int index, int? timeoutMs = null)
        => WaitFor(() => TryItem(index), $"row {index}", timeoutMs);

    /// <summary>The row of item <paramref name="key"/>, waiting for it, as one call.</summary>
    /// <exception cref="ElementNotFoundException">There was no such item within the budget.</exception>
    public TItem Item(string key, int? timeoutMs = null)
        => WaitFor(() => TryItem(key), $"item '{key}'", timeoutMs);

    /// <summary>The first row that matches <paramref name="predicate"/>, waiting for one, as one call.</summary>
    /// <exception cref="ElementNotFoundException">No row matched within the budget.</exception>
    public TItem ItemWhere(Func<TItem, bool> predicate, int? timeoutMs = null)
        => WaitFor(() => Items.FirstOrDefault(predicate), "a row that matches", timeoutMs);

    /// <summary>
    /// Waits until no row matches <paramref name="predicate"/>. A list that is not there at all has
    /// no rows, so its missing root is an answer, not a wait.
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

    // One call: each attempt looks once.
    private TItem WaitFor(Func<TItem?> find, string what, int? timeoutMs)
    {
        TItem? found = null;
        var budget = timeoutMs ?? DefaultTimeoutMs;
        RunProbe(
            () => ScopeGate.Check(ProbeReadiness())
                ?? ((found = find()) != null ? Observation.Done() : Observation.Missing(what)),
            budget,
            last => new ElementNotFoundException($"{ScopeName} had no {what} within {budget} ms. Last seen: {last}."));
        return found!;
    }
}
