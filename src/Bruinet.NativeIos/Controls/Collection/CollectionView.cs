using Bruinet.NativeIos.Collections;

namespace Bruinet.NativeIos.Controls.Collection;

/// <summary>
/// A <c>UICollectionView</c> whose rows are its cells, as <see cref="ListItem"/>s. XCUITest keeps
/// only the cells on the screen in the tree, so the collection scrolls to reach the others. For rows
/// with controls of their own, derive from <see cref="CollectionObjectBase{TParent, TSelf, TItem}"/>
/// with a row type.
/// </summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
public class CollectionView<TParent> : CollectionObjectBase<TParent, CollectionView<TParent>, ListItem>
    where TParent : INativeIosScope<TParent>
{
    private readonly Locator? _itemKey;

    /// <summary>The collection found by <paramref name="locator"/>; <paramref name="itemKey"/> is the element whose text keys a row.</summary>
    public CollectionView(INativeIosScope<TParent> parent, Locator locator, Locator? itemKey = null)
        : base(parent, locator, Cells.Locator)
    {
        _itemKey = itemKey;
    }

    /// <summary>The collection with accessibility identifier <paramref name="identifier"/>; <paramref name="itemKeyId"/> is the element whose text keys a row.</summary>
    public CollectionView(INativeIosScope<TParent> parent, string identifier, string? itemKeyId = null)
        : this(parent, Locator.ById(identifier), itemKeyId is null ? null : Locator.ById(itemKeyId))
    {
    }

    /// <inheritdoc />
    public override CollectionView<TParent> Self => this;

    /// <inheritdoc />
    protected override Locator? ItemKeyLocator => _itemKey;

    /// <inheritdoc />
    protected override ListItem CreateItem(ItemKey key, int position) => new(this, key, position);
}

/// <summary>A <c>UITableView</c> whose rows are its cells, as <see cref="ListItem"/>s; the same as <see cref="CollectionView{TParent}"/>.</summary>
/// <typeparam name="TParent">The scope it is declared in.</typeparam>
public class TableView<TParent> : CollectionObjectBase<TParent, TableView<TParent>, ListItem>
    where TParent : INativeIosScope<TParent>
{
    private readonly Locator? _itemKey;

    /// <summary>The table found by <paramref name="locator"/>; <paramref name="itemKey"/> is the element whose text keys a row.</summary>
    public TableView(INativeIosScope<TParent> parent, Locator locator, Locator? itemKey = null)
        : base(parent, locator, Cells.Locator)
    {
        _itemKey = itemKey;
    }

    /// <summary>The table with accessibility identifier <paramref name="identifier"/>; <paramref name="itemKeyId"/> is the element whose text keys a row.</summary>
    public TableView(INativeIosScope<TParent> parent, string identifier, string? itemKeyId = null)
        : this(parent, Locator.ById(identifier), itemKeyId is null ? null : Locator.ById(itemKeyId))
    {
    }

    /// <inheritdoc />
    public override TableView<TParent> Self => this;

    /// <inheritdoc />
    protected override Locator? ItemKeyLocator => _itemKey;

    /// <inheritdoc />
    protected override ListItem CreateItem(ItemKey key, int position) => new(this, key, position);
}

// A table or collection also holds headers, footers and its scroll bars: its rows are its cells.
// A class-name lookup is a cheap query; "./*" is an XPath, which reads the whole page source.
internal static class Cells
{
    public static readonly Locator Locator = Locator.ByClassName("XCUIElementTypeCell");
}
