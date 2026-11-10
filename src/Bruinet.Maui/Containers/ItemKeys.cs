using System.Globalization;
using Bruinet.Core.Collections;

namespace Bruinet.Maui.Containers;

/// <summary>How MAUI checks an <see cref="ItemKey"/> against a row; the key itself is Core's.</summary>
internal static class ItemKeys
{
    /// <summary>The index, for a logical or position key.</summary>
    internal static int Index(this ItemKey key) => int.Parse(key.Value, CultureInfo.InvariantCulture);

    /// <summary>
    /// Whether <paramref name="itemRoot"/> still holds this item, as far as the element can say.
    /// </summary>
    /// <remarks>
    /// One read for a logical or id key. A position key cannot be checked, so it always answers
    /// yes. A removed element answers with <see cref="StaleElementException"/>.
    /// </remarks>
    internal static bool IsHeldBy(this ItemKey key, IMauiElement itemRoot) => key.Kind switch
    {
        ItemKeyKind.Logical => LogicalIndexOf(itemRoot) == key.Index(),
        ItemKeyKind.AutomationId => itemRoot.AutomationId == key.Value,
        _ => true
    };

    /// <summary>
    /// The row's logical index (<c>PositionInSet</c>, 1-based on the platform), or null when the
    /// platform does not publish one.
    /// </summary>
    internal static int? LogicalIndexOf(IMauiElement itemRoot)
        => itemRoot.PositionInSet is > 0 and var position ? position - 1 : null;
}
