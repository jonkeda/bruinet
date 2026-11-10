using System.Globalization;

namespace Bruinet.Core.Collections;

/// <summary>What an <see cref="ItemKey"/> identifies a row by, strongest first.</summary>
public enum ItemKeyKind
{
    /// <summary>
    /// By its item: a key read from the row (a <c>data-item-key</c>, a key element's text) or the
    /// item's index in the data. Survives scrolling, re-rendering and row recycling.
    /// </summary>
    Logical,

    /// <summary>By the row's automation id, where the id names the item (<c>Task_0</c>, <c>Task_1</c>).</summary>
    AutomationId,

    /// <summary>
    /// By its place among the rows shown. The weakest key: a recycled row found again by position
    /// may hold another item, and nothing on the element can tell. Use only on fixed lists.
    /// </summary>
    Position,
}

/// <summary>
/// Which item a row object stands for, recorded when the row object is made. A row re-finds its
/// element by this key on every lookup, so it follows its item and never acts on another (R0).
/// Every stack's collections use this one type; how a key is read from a row is the stack's.
/// </summary>
/// <param name="Kind">What the key identifies the row by.</param>
/// <param name="Value">The key read from the row, the logical index, the automation id, or the position, as text.</param>
public readonly record struct ItemKey(ItemKeyKind Kind, string Value)
{
    /// <summary>A logical key read from the row.</summary>
    public static ItemKey Logical(string key) => new(ItemKeyKind.Logical, key);

    /// <summary>A logical key that is the item's index in the data.</summary>
    public static ItemKey Logical(int index) => new(ItemKeyKind.Logical, index.ToString(CultureInfo.InvariantCulture));

    /// <summary>An automation-id key.</summary>
    public static ItemKey AutomationId(string automationId) => new(ItemKeyKind.AutomationId, automationId);

    /// <summary>A position key.</summary>
    public static ItemKey Position(int position) => new(ItemKeyKind.Position, position.ToString(CultureInfo.InvariantCulture));

    /// <summary>"item 'Invoice 12'", "id Task_3" or "row 3", for messages.</summary>
    public override string ToString() => Kind switch
    {
        ItemKeyKind.Logical => $"item '{Value}'",
        ItemKeyKind.AutomationId => $"id {Value}",
        _ => $"row {Value}",
    };
}
