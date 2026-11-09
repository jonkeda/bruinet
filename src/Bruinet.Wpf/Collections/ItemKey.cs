namespace Bruinet.Wpf.Collections;

/// <summary>How an item row is identified (design 7).</summary>
public enum ItemKeyKind
{
    /// <summary>By its item: a key read from the row. Survives scrolling and row recycling.</summary>
    Logical,

    /// <summary>By its place among the rows shown. Cannot tell a recycled row from its old item: use only on fixed lists.</summary>
    Position,
}

/// <summary>
/// Which item a row object stands for. A row re-finds its element by this key on every lookup, so
/// it follows its item when a virtualizing list recycles its containers, and never acts on another
/// (R0).
/// </summary>
/// <param name="Kind">How the row is identified.</param>
/// <param name="Value">The key read from the row, or the position as text.</param>
public readonly record struct ItemKey(ItemKeyKind Kind, string Value)
{
    private static readonly Locator FirstText = Locator.ByControlType("Text");

    /// <summary>
    /// The key of <paramref name="row"/>: the text of its <paramref name="keyElement"/> when the
    /// collection declares one, else its first text block, else its name, else its position. A
    /// row's name is WPF's <c>ToString()</c> of its item unless the app sets it, so it comes last.
    /// </summary>
    public static ItemKey Of(IWpfElement row, int position, Locator? keyElement = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = (keyElement is null ? null : Present(row.TryFindElement(keyElement)?.Text))
            ?? Present(row.TryFindElement(FirstText)?.Name)
            ?? Present(row.Name);
        return key is null
            ? new ItemKey(ItemKeyKind.Position, position.ToString(CultureInfo.InvariantCulture))
            : new ItemKey(ItemKeyKind.Logical, key);
    }

    /// <summary>"item 'Invoice 12'" or "row 3", for messages.</summary>
    public override string ToString() => Kind == ItemKeyKind.Logical ? $"item '{Value}'" : $"row {Value}";

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
