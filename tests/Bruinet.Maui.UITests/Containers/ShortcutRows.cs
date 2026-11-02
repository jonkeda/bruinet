using Bruinet.Maui.Containers;
using Bruinet.Maui.Controls.Base;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Containers;

/// <summary>Rows opened by a tap on the row: a recognizer whose command is bound through x:Reference to the page.</summary>
public class ShortcutRows : CollectionObjectBase<GridCollectionDemoPage, ShortcutRows, ShortcutRow>
{
    public ShortcutRows(IMauiScope<GridCollectionDemoPage> parentScope)
        : base(parentScope,
               "ShortcutCollectionView",
               ItemStrategy.ByAutomationId("ShortcutRow"),
               (collection, itemRoot, index) => new ShortcutRow(collection, itemRoot, index))
    {
    }

    public ShortcutRow Row(string name, int? timeoutMs = null) => ItemWhere(row => row.Name.GetText() == name, timeoutMs);
}

/// <summary>A shortcut row: clicked as a whole.</summary>
public class ShortcutRow : ClickableItemBase<ShortcutRows, ShortcutRow>
{
    public ShortcutRow(ShortcutRows collection, IMauiElement itemRoot, int index)
        : base(collection, itemRoot, index)
    {
    }

    public Label<ShortcutRow> Name => new(this, "ShortcutNameLabel");
}
