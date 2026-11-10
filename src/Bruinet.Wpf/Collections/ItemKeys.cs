namespace Bruinet.Wpf.Collections;

/// <summary>How this stack reads an <see cref="ItemKey"/> from a row; the key itself is Core's.</summary>
internal static class ItemKeys
{
    private static readonly Locator FirstText = Locator.ByControlType("Text");

    /// <summary>
    /// The key of <paramref name="row"/>: the text of its <paramref name="keyElement"/> when the
    /// collection declares one, else its first text block, else its name, else its position. A
    /// row's name is WPF's <c>ToString()</c> of its item unless the app sets it, so it comes last.
    /// </summary>
    internal static ItemKey Of(IWpfElement row, int position, Locator? keyElement = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = (keyElement is null ? null : Present(row.TryFindElement(keyElement)?.Text))
            ?? Present(row.TryFindElement(FirstText)?.Name)
            ?? Present(row.Name);
        return key is null
            ? ItemKey.Position(position)
            : ItemKey.Logical(key);
    }

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
