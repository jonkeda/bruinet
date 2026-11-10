using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Interfaces;

namespace Bruinet.NativeAndroid.Collections;

/// <summary>
/// A list of item rows that hands out typed row objects: Core's
/// <see cref="CollectionObjectBase{TContext, TElement, TParentScope, TSelf, TItem}"/> on the NativeAndroid types,
/// with where NativeAndroid's rows are, how a row's key is read, and how its lists scroll.
/// </summary>
/// <typeparam name="TParent">The scope the collection is declared in.</typeparam>
/// <typeparam name="TSelf">The collection type, for fluent chains.</typeparam>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class CollectionObjectBase<TParent, TSelf, TItem>
    : CollectionObjectBase<INativeAndroidTestContext, INativeAndroidElement, INativeAndroidScope<TParent>, TSelf, TItem>, INativeAndroidContainer<TParent, TSelf>, INativeAndroidCollection
    where TParent : INativeAndroidScope<TParent>
    where TSelf : CollectionObjectBase<TParent, TSelf, TItem>
    where TItem : ItemObjectBase<TItem>
{
    /// <summary>A list's rows: its direct children.</summary>
    protected static readonly Locator DirectChildren = Locator.ByXPath("./*");

    private readonly Locator _itemLocator;

    /// <summary>The list found by <paramref name="locator"/>, whose rows match <paramref name="itemLocator"/> inside it (<see cref="DirectChildren"/> by default).</summary>
    protected CollectionObjectBase(INativeAndroidScope<TParent> parent, Locator locator, Locator? itemLocator = null)
        : base(parent, locator)
    {
        _itemLocator = itemLocator ?? DirectChildren;
    }

    /// <summary>The list whose resourceId is <paramref name="resourceId"/>.</summary>
    protected CollectionObjectBase(INativeAndroidScope<TParent> parent, string resourceId, Locator? itemLocator = null)
        : this(parent, Locator.ById(resourceId), itemLocator)
    {
    }

    /// <summary>The element inside a row whose text is the row's key; null to use the stack's fallbacks.</summary>
    protected virtual Locator? ItemKeyLocator => null;

    /// <inheritdoc />
    public override INativeAndroidTestContext Context => ParentScope.Context;

    /// <inheritdoc />
    public TParent Parent => ParentScope.Self;

    /// <inheritdoc />
    public INativeAndroidPage? Page => ParentScope.Page;

    /// <inheritdoc />
    /// <remarks>
    /// Once per call, before scrolling by hand, Android is asked to scroll the item's text into view
    /// (UiScrollable), which finds a row far down a long list in one step.
    /// </remarks>
    public override TItem Item(string key, int? timeoutMs = null)
    {
        var swept = !ScrollsToFind;
        return WaitFor(() =>
        {
            if (TryItem(key) is { } shown)
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
    protected override ItemKey KeyOf(INativeAndroidElement row, int position) => ItemKeys.Of(row, position, ItemKeyLocator);

    /// <inheritdoc />
    protected override IReadOnlyList<INativeAndroidElement> TryGetItemRoots() => FindElements(_itemLocator);

    /// <inheritdoc />
    protected override bool ScrollsToFind => true;

    /// <inheritdoc />
    protected override bool ScrollStep(INativeAndroidElement list, bool onwards) => list.Scroll(onwards ? GestureDirection.Up : GestureDirection.Down);

    /// <inheritdoc />
    protected override bool IsShown(INativeAndroidElement root) => root.Visible;

    /// <inheritdoc />
    protected override INativeAndroidElement? TryFindIn(INativeAndroidElement root, Locator locator) => root.TryFindElement(locator);

    /// <inheritdoc />
    protected override IReadOnlyList<INativeAndroidElement> FindAllIn(INativeAndroidElement root, Locator locator) => root.FindElements(locator);
}
