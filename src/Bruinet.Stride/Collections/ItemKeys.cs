namespace Bruinet.Stride.Collections;

/// <summary>How this stack reads an <see cref="ItemKey"/> from a row; the key itself is Core's.</summary>
internal static class ItemKeys
{
    /// <summary>
    /// The key of <paramref name="row"/>: the text of its <paramref name="keyElement"/> when the
    /// collection declares one, else its own text (a text block row), else its first child's text,
    /// else its name, else its position.
    /// </summary>
    internal static ItemKey Of(IStrideElement row, int position, Locator? keyElement = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = (keyElement is null ? null : Present(row.TryFindElement(keyElement)?.Text))
            ?? Present(row.Text)
            ?? row.Children().Select(child => Present(child.Text)).FirstOrDefault(text => text is not null)
            ?? Present(row.AutomationId);
        return key is null
            ? ItemKey.Position(position)
            : ItemKey.Logical(key);
    }

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
