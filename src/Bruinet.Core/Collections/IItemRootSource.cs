namespace Bruinet.Core.Collections;

/// <summary>
/// A collection that hands out its rows by key: what a row object asks for its root on every lookup
/// (public API review, proposal 5). Each stack's collection interface extends it with its element type.
/// </summary>
/// <typeparam name="TElement">The stack's element.</typeparam>
public interface IItemRootSource<TElement>
    where TElement : class
{
    /// <summary>The row element of <paramref name="key"/> now, or <c>null</c> when it is not shown. One attempt.</summary>
    TElement? TryGetItemRoot(ItemKey key);
}
