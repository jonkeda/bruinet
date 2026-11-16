using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Appium;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Collections;

/// <summary>
/// A list of item rows that hands out typed row objects: Core's
/// <see cref="CollectionObjectBase{TContext, TElement, TParentScope, TSelf, TItem}"/> on the NativeIos types,
/// with where NativeIos's rows are, how a row's key is read, and how its lists scroll.
/// </summary>
/// <typeparam name="TParent">The scope the collection is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TParent, TSelf, TItem>
    : CollectionObjectBase<INativeIosTestContext, INativeIosElement, INativeIosScope<TParent>, TSelf, TItem>, INativeIosContainer<TParent, TSelf>, INativeIosCollection
    where TParent : INativeIosScope<TParent>
    where TSelf : CollectionObjectBase<TParent, TSelf, TItem>
    where TItem : ItemObjectBase<TItem>
{
    /// <summary>A list's rows: its direct children.</summary>
    protected static readonly Locator DirectChildren = Locator.ByXPath("./*");

    private readonly Locator _itemLocator;

    /// <summary>The list found by <paramref name="locator"/>, whose rows match <paramref name="itemLocator"/> inside it (<see cref="DirectChildren"/> by default).</summary>
    protected CollectionObjectBase(INativeIosScope<TParent> parent, Locator locator, Locator? itemLocator = null)
        : base(parent, locator)
    {
        _itemLocator = itemLocator ?? DirectChildren;
    }

    /// <summary>The list whose accessibility identifier is <paramref name="identifier"/>.</summary>
    protected CollectionObjectBase(INativeIosScope<TParent> parent, string identifier, Locator? itemLocator = null)
        : this(parent, Locator.ById(identifier), itemLocator)
    {
    }

    /// <summary>The element inside a row whose text is the row's key; null to use the stack's fallbacks.</summary>
    protected virtual Locator? ItemKeyLocator => null;

    /// <inheritdoc />
    public override INativeIosTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public INativeIosPage? Page => ParentScope.Page;

    /// <inheritdoc />
    /// <remarks>
    /// A table keeps all its rows in the tree, so a row far down is found at once but is not on the
    /// screen: it is then scrolled into view within the call's budget, since the views inside a row off
    /// the screen report a wrong position (measured, 2026-11-16). A row not in the tree is looked
    /// for once in the list's tree and scrolled to, before scrolling by hand.
    /// </remarks>
    public override TItem Item(string key, int? timeoutMs = null)
    {
        var deadline = Environment.TickCount64 + (timeoutMs ?? DefaultTimeoutMs);
        var swept = !ScrollsToFind;
        return WaitFor(() =>
        {
            if (RowsChain() is { } rows)
            {
                if (FastRow(rows, key) is { } row)
                {
                    if (ScrollsToFind && !row.Visible)
                    {
                        row.ScrollIntoView((int)Math.Max(0, deadline - Environment.TickCount64));
                    }

                    return CreateItem(ItemKey.Logical(key), -1);
                }
            }
            else if (TryItem(key) is { } shown)
            {
                return shown;
            }

            if (!swept)
            {
                swept = true;
                Context.Driver.TryScrollIntoView(Locator.ByText(key), Locator);
                return TryItem(key);
            }

            return null;
        }, $"item '{key}'", ScrollsToFind, timeoutMs);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Asks XCUITest for the rows that hold the key's text, rather than reading every row: a table
    /// keeps all its rows in the tree, and each row read costs about one tree snapshot (a second or more
    /// on a list of 200, measured 2026-11). The rows found are then checked as usual. Such a row's
    /// <c>Position</c> is -1: its place among the rows is not read.
    /// </remarks>
    public override TItem? TryItem(string key)
    {
        if (RowsChain() is not { } rows)
        {
            return base.TryItem(key);
        }

        return FastRow(rows, key) is not null ? CreateItem(ItemKey.Logical(key), -1) : null;
    }

    /// <inheritdoc />
    /// <remarks>A row with a text key is found as <see cref="TryItem(string)"/> finds it.</remarks>
    public override INativeIosElement? TryGetItemRoot(ItemKey key)
        => key.Kind == ItemKeyKind.Logical && RowsChain() is { } rows
            ? FastRow(rows, key.Value)
            : base.TryGetItemRoot(key);

    // The first row holding the key's text whose key it is.
    private INativeIosElement? FastRow(string rows, string key)
    {
        var wanted = ItemKey.Logical(key);
        return RowsHolding(rows, key).FirstOrDefault(row => KeyOf(row, -1) == wanted);
    }

    // The rows' locator as a class chain step; null when it has none, and then every row is read.
    private string? RowsChain()
        => _itemLocator == DirectChildren ? "*"
            : _itemLocator.Strategy is LocatorStrategy.ClassName or LocatorStrategy.TagName or LocatorStrategy.ControlType ? "**/" + _itemLocator.Value
            : null;

    // Lazily: a row holding a text with the key, then (without a key view) a row labelled with it.
    private IEnumerable<INativeIosElement> RowsHolding(string rows, string key)
    {
        if (TryFindRootElement() is not { } list)
        {
            yield break;
        }

        var literal = NativeIosByExtensions.ToPredicateLiteral(key);
        var text = $"(label == {literal} OR value == {literal})";
        var holder = ItemKeyLocator?.ToPredicate() is { } keyView
            ? $"({keyView}) AND {text}"
            : $"type == 'XCUIElementTypeStaticText' AND {text}";
        foreach (var row in list.FindByClassChain($"{rows}[${holder}$]"))
        {
            yield return row;
        }

        if (ItemKeyLocator is null)
        {
            foreach (var row in list.FindByClassChain($"{rows}[`{text}`]"))
            {
                yield return row;
            }
        }
    }

    /// <inheritdoc />
    protected override ItemKey KeyOf(INativeIosElement row, int position) => ItemKeys.Of(row, position, ItemKeyLocator);

    /// <inheritdoc />
    protected override IReadOnlyList<INativeIosElement> TryGetItemRoots() => FindElements(_itemLocator);

    /// <inheritdoc />
    protected override bool ScrollsToFind => true;

    /// <inheritdoc />
    protected override bool ScrollStep(INativeIosElement list, bool onwards) => list.Scroll(onwards ? GestureDirection.Up : GestureDirection.Down);

    /// <inheritdoc />
    protected override bool IsShown(INativeIosElement root) => root.Visible;

    /// <inheritdoc />
    protected override INativeIosElement? TryFindIn(INativeIosElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<INativeIosElement> FindAllIn(INativeIosElement root, Locator locator) => root.FindElements(locator);
}
