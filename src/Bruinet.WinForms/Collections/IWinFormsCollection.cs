namespace Bruinet.WinForms.Collections;

/// <summary>
/// A scope that hands out item rows: what an <see cref="ItemObjectBase{TSelf}"/> asks for its root.
/// </summary>
public interface IWinFormsCollection : IWinFormsElementScope
{
    /// <summary>The row element of <paramref name="key"/> now, or <c>null</c> when it is not shown. One attempt.</summary>
    IWinFormsElement? TryGetItemRoot(ItemKey key);
}
