using Bruinet.NativeIos.Collections;
using Bruinet.NativeIos.Controls.Buttons;
using Bruinet.NativeIos.Controls.Text;
using Bruinet.NativeIos.Controls.Toggle;

namespace Bruinet.NativeIos.Controls.Collection;

/// <summary>
/// A row of a <see cref="TableView{TParent}"/> or <see cref="CollectionView{TParent}"/>, when the
/// page object needs no row type of its own: the cell as a button, and its elements by
/// accessibility identifier.
/// </summary>
public sealed class ListItem : ItemObjectBase<ListItem>
{
    /// <summary>The row of <paramref name="key"/> in <paramref name="collection"/>.</summary>
    public ListItem(INativeIosCollection collection, ItemKey key, int position)
        : base(collection, key, position)
    {
    }

    /// <inheritdoc />
    public override ListItem Self => this;

    /// <summary>The cell itself, to tap or long-press.</summary>
    public Button<ListItem> Row => new(this, Locator.ByXPath("."));

    /// <summary>A text in the row, by accessibility identifier.</summary>
    public Label<ListItem> Text(string identifier) => new(this, identifier);

    /// <summary>A button in the row, by accessibility identifier.</summary>
    public Button<ListItem> Button(string identifier) => new(this, identifier);

    /// <summary>A switch in the row, by accessibility identifier.</summary>
    public Switch<ListItem> Switch(string identifier) => new(this, identifier);
}
