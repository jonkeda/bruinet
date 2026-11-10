using Bruinet.Core.Collections;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Collections;

/// <summary>
/// A scope that hands out item rows: what an <see cref="ItemObjectBase{TSelf}"/> asks for its root.
/// </summary>
public interface IHtmlCollection : IHtmlElementScope
{
    /// <summary>The row element of <paramref name="key"/> now, or <c>null</c> when it is not in the list. One attempt.</summary>
    IHtmlElement? TryGetItemRoot(ItemKey key);
}
