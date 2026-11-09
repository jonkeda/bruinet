namespace Bruinet.Wpf.Collections;

/// <summary>
/// A scope that hands out item rows: what an <see cref="ItemObjectBase{TSelf}"/> asks for its root.
/// </summary>
public interface IWpfCollection : IWpfElementScope
{
    /// <summary>The row element of <paramref name="key"/> now, or <c>null</c> when it is not shown. One attempt.</summary>
    IWpfElement? TryGetItemRoot(ItemKey key);
}
