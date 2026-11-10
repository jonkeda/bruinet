using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Collection;

/// <summary>
/// HTML list control. Wraps &lt;ul&gt; and &lt;ol&gt;, reading their &lt;li&gt; items as text. For
/// rows that are acted on, use a collection (<c>CollectionObjectBase</c>).
/// </summary>
public partial class ListControl<TScope> : ViewBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    public ListControl(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ListControl(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    private static readonly Locator Items = Locator.ByCss("li");

    /// <summary>How many items the list has.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual int? GetItemCountCore(IHtmlElement element)
    {
        return element.FindElements(Items).Count;
    }

    /// <summary>The text of the item at <paramref name="index"/> (0-based); null when there is none.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="index">The item's position.</param>
    protected virtual string? GetItemTextCore(IHtmlElement element, int index)
    {
        var items = element.FindElements(Items);
        return index >= 0 && index < items.Count ? items[index].Text : null;
    }

    /// <summary>The items' texts, in order.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.SequenceEquals | Comparison.HasItem | Comparison.Count)]
    protected virtual IReadOnlyList<string>? GetItemTextsCore(IHtmlElement element)
    {
        return element.FindElements(Items).Select(item => item.Text ?? string.Empty).ToList();
    }
}
