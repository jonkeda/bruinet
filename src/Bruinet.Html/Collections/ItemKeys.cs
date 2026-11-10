using Bruinet.Core.Collections;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Collections;

/// <summary>How Html reads an <see cref="ItemKey"/> from a row: its <c>data-item-key</c>, or its position (design 7.6).</summary>
internal static class ItemKeys
{
    /// <summary>The attribute a list row carries its item's key in.</summary>
    internal const string Attribute = "data-item-key";

    /// <summary>The key of <paramref name="row"/>: its <c>data-item-key</c>, or its position.</summary>
    internal static ItemKey Of(IHtmlElement row, int position)
        => row.GetAttribute(Attribute) is { Length: > 0 } key ? ItemKey.Logical(key) : ItemKey.Position(position);
}
