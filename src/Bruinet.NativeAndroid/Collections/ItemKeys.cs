namespace Bruinet.NativeAndroid.Collections;

/// <summary>How this stack reads an <see cref="ItemKey"/> from a row; the key itself is Core's.</summary>
internal static class ItemKeys
{
    private static readonly Locator FirstText = Locator.ByClassName("android.widget.TextView");

    /// <summary>
    /// The key of <paramref name="row"/>: the text of its <paramref name="keyView"/> when the
    /// collection declares one, else its content description, else its first text, else its position.
    /// </summary>
    internal static ItemKey Of(INativeAndroidElement row, int position, Locator? keyView = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = (keyView is null ? null : Present(row.TryFindElement(keyView)?.Text))
            ?? Present(row.ContentDescription)
            ?? Present(row.TryFindElement(FirstText)?.Text);
        return key is null
            ? ItemKey.Position(position)
            : ItemKey.Logical(key);
    }

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
