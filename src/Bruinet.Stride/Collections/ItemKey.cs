namespace Bruinet.Stride.Collections;

/// <summary>How an item row is identified (design 7).</summary>
public enum ItemKeyKind
{
    /// <summary>By its item: a key read from the row. Survives the list being rebuilt.</summary>
    Logical,

    /// <summary>By its place among the rows. Cannot tell a rebuilt row from its old item: use only on fixed lists.</summary>
    Position,
}

/// <summary>
/// Which item a row object stands for. A row re-finds its element by this key on every lookup, so it
/// follows its item when the game rebuilds the list, and never acts on another (R0).
/// </summary>
/// <param name="Kind">How the row is identified.</param>
/// <param name="Value">The key read from the row, or the position as text.</param>
public readonly record struct ItemKey(ItemKeyKind Kind, string Value)
{
    /// <summary>
    /// The key of <paramref name="row"/>: the text of its <paramref name="keyElement"/> when the
    /// collection declares one, else its own text (a text block row), else its first child's text,
    /// else its name, else its position.
    /// </summary>
    public static ItemKey Of(IStrideElement row, int position, Locator? keyElement = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = (keyElement is null ? null : Present(row.TryFindElement(keyElement)?.Text))
            ?? Present(row.Text)
            ?? row.Children().Select(child => Present(child.Text)).FirstOrDefault(text => text is not null)
            ?? Present(row.AutomationId);
        return key is null
            ? new ItemKey(ItemKeyKind.Position, position.ToString(CultureInfo.InvariantCulture))
            : new ItemKey(ItemKeyKind.Logical, key);
    }

    /// <summary>"item 'Apples'" or "row 3", for messages.</summary>
    public override string ToString() => Kind == ItemKeyKind.Logical ? $"item '{Value}'" : $"row {Value}";

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
