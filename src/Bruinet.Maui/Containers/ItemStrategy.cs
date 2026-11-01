namespace Bruinet.Maui.Containers;

/// <summary>
/// Decides how a collection discovers its item root elements. How items are found is a
/// policy that varies by platform and by how the app marks up its rows, so it is
/// pluggable rather than fixed.
/// </summary>
public interface IItemStrategy
{
    /// <summary>
    /// Finds the root element of every currently materialized item, in document order.
    /// </summary>
    IReadOnlyList<IMauiElement> FindItemElements(IMauiElement collectionRoot);

    /// <summary>
    /// Finds one item's root, or null when the index is out of range.
    /// </summary>
    IMauiElement? FindItemElement(IMauiElement collectionRoot, int index);

    /// <summary>
    /// Whether each row's automation id names its item and no other (<c>Task_0</c>,
    /// <c>Task_1</c>): a row can then be found again by its id.
    /// </summary>
    bool HasStableIds => false;
}

/// <summary>
/// Factory methods for the built-in item strategies.
/// </summary>
public static class ItemStrategy
{
    /// <summary>
    /// Finds rows by a locator applied within the collection root. The locator's value
    /// repeats across rows, which is the normal shape of a MAUI item template.
    /// </summary>
    public static IItemStrategy ByLocator(Locator itemLocator) => new LocatorItemStrategy(itemLocator);

    /// <summary>
    /// Finds rows by automation id, within the collection root.
    /// </summary>
    public static IItemStrategy ByAutomationId(string automationId)
        => new LocatorItemStrategy(Locator.ByAutomationId(automationId));

    /// <summary>
    /// Narrows another strategy to a host element inside the collection root.
    /// </summary>
    /// <remarks>
    /// A collection's root is often not the element holding the rows - a menu's root holds a
    /// trigger as well as its items, and an automation wrapper holds the real list. Composing
    /// says where to look without teaching every strategy about hosts:
    /// <c>ItemStrategy.Within(Locator.ByAutomationId("ActionsMenuItems"), inner)</c>. A host
    /// that is not there yields no items, which is the right answer for a menu that is closed.
    /// </remarks>
    /// <param name="hostLocator">The element inside the root that holds the items.</param>
    /// <param name="inner">How to find items once the host is found.</param>
    public static IItemStrategy Within(Locator hostLocator, IItemStrategy inner)
        => new WithinItemStrategy(hostLocator, inner);

    /// <summary>
    /// Finds rows by a locator, inside a host element within the collection root.
    /// </summary>
    public static IItemStrategy Within(Locator hostLocator, Locator itemLocator)
        => new WithinItemStrategy(hostLocator, ByLocator(itemLocator));

    /// <summary>
    /// Finds rows by a per-index automation id (<c>Task_0</c>, <c>Task_1</c>, ...),
    /// searched within the collection root.
    /// </summary>
    /// <remarks>
    /// Requires the app to give every row a globally unique id. Prefer
    /// <see cref="ByLocator"/>, which lets an item template keep repeating ids.
    /// Enumeration probes one index at a time and stops at <paramref name="maxItems"/>,
    /// so a collection longer than that reports a truncated count.
    /// </remarks>
    public static IItemStrategy ByIndexedId(string prefix, int maxItems = 1000)
        => new IndexedIdItemStrategy(prefix, maxItems);
}

/// <summary>
/// Finds item roots by applying one locator inside the collection root.
/// </summary>
internal sealed class LocatorItemStrategy : IItemStrategy
{
    private readonly Locator _itemLocator;

    public LocatorItemStrategy(Locator itemLocator)
        => _itemLocator = itemLocator ?? throw new ArgumentNullException(nameof(itemLocator));

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindItemElements(IMauiElement collectionRoot)
    {
        ArgumentNullException.ThrowIfNull(collectionRoot);

        return collectionRoot.FindElements(_itemLocator);
    }

    /// <inheritdoc />
    public IMauiElement? FindItemElement(IMauiElement collectionRoot, int index)
    {
        if (index < 0)
            return null;

        var items = FindItemElements(collectionRoot);
        return index < items.Count ? items[index] : null;
    }
}

/// <summary>
/// Finds item roots by a per-index automation id.
/// </summary>
internal sealed class IndexedIdItemStrategy : IItemStrategy
{
    /// <inheritdoc />
    public bool HasStableIds => true;

    private readonly string _prefix;
    private readonly int _maxItems;

    public IndexedIdItemStrategy(string prefix, int maxItems)
    {
        _prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));

        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxItems), maxItems, "Must be greater than zero.");

        _maxItems = maxItems;
    }

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindItemElements(IMauiElement collectionRoot)
    {
        ArgumentNullException.ThrowIfNull(collectionRoot);

        var items = new List<IMauiElement>();
        for (var index = 0; index < _maxItems; index++)
        {
            var element = FindItemElement(collectionRoot, index);
            if (element == null)
                break;

            items.Add(element);
        }

        return items;
    }

    /// <inheritdoc />
    public IMauiElement? FindItemElement(IMauiElement collectionRoot, int index)
    {
        ArgumentNullException.ThrowIfNull(collectionRoot);

        if (index < 0)
            return null;

        try
        {
            return collectionRoot.FindElement(
                Locator.ByAutomationId($"{_prefix}{index}"));
        }
        catch (ElementNotFoundException)
        {
            return null;
        }
    }
}

/// <summary>
/// Applies another strategy inside a host element found within the collection root.
/// </summary>
internal sealed class WithinItemStrategy : IItemStrategy
{
    private readonly Locator _hostLocator;
    private readonly IItemStrategy _inner;

    /// <inheritdoc />
    public bool HasStableIds => _inner.HasStableIds;

    public WithinItemStrategy(Locator hostLocator, IItemStrategy inner)
    {
        _hostLocator = hostLocator ?? throw new ArgumentNullException(nameof(hostLocator));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindItemElements(IMauiElement collectionRoot)
    {
        var host = TryFindHost(collectionRoot);
        return host == null ? [] : _inner.FindItemElements(host);
    }

    /// <inheritdoc />
    public IMauiElement? FindItemElement(IMauiElement collectionRoot, int index)
    {
        if (index < 0)
            return null;

        var host = TryFindHost(collectionRoot);
        return host == null ? null : _inner.FindItemElement(host, index);
    }

    /// <summary>
    /// The host, or null when it is not there - a hidden host leaves the tree on Android and
    /// is collapsed out of it on Windows, and either way the collection is empty.
    /// </summary>
    private IMauiElement? TryFindHost(IMauiElement collectionRoot)
    {
        ArgumentNullException.ThrowIfNull(collectionRoot);

        return collectionRoot.TryFindElement(_hostLocator);
    }
}
