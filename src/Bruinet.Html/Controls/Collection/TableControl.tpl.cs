using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Collection;

/// <summary>
/// HTML table control. Wraps &lt;table&gt;, reading its header and body cells as text.
/// </summary>
public partial class TableControl<TScope> : ViewBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    public TableControl(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TableControl(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    /// <summary>How many body rows the table has.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual int? GetRowCountCore(IHtmlElement element)
    {
        return element.FindElements(Locator.ByCss("tbody tr")).Count;
    }

    /// <summary>Waits until the table has at least one body row: rows often load after the table.</summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    public bool WaitHasRows(int? timeoutMs = null)
        => RunWaitWithElement(true, element => GetRowCountCore(element) > 0, timeoutMs);

    /// <summary>Asserts the table has at least one body row, waiting for its rows to load.</summary>
    /// <param name="message">Optional failure message.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    public TScope AssertHasRows(string? message = null, int? timeoutMs = null)
        => RunAssertWithElement(true, element => GetRowCountCore(element) > 0, (actual, expected) => actual == expected,
            message ?? $"Expected the table {Locator} to have rows.", timeoutMs);

    /// <summary>How many columns the table has: its header cells, or else its first row's cells.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual int? GetColumnCountCore(IHtmlElement element)
    {
        var headerCells = element.FindElements(Locator.ByCss("thead th"));
        return headerCells.Count > 0
            ? headerCells.Count
            : element.FindElements(Locator.ByCss("tbody tr:first-child td")).Count;
    }

    /// <summary>The text of one body cell (0-based); null when there is none.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="row">The row's position.</param>
    /// <param name="column">The column's position.</param>
    protected virtual string? GetCellTextCore(IHtmlElement element, int row, int column)
    {
        if (row < 0 || column < 0)
        {
            return null;
        }

        var cells = element.FindElements(Locator.ByCss($"tbody tr:nth-child({row + 1}) td"));
        return column < cells.Count ? cells[column].Text : null;
    }

    /// <summary>The text of one header cell (0-based); null when there is none.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="column">The column's position.</param>
    protected virtual string? GetHeaderTextCore(IHtmlElement element, int column)
    {
        if (column < 0)
        {
            return null;
        }

        var headers = element.FindElements(Locator.ByCss("thead th"));
        return column < headers.Count ? headers[column].Text : null;
    }

    /// <summary>The texts of one body row's cells (0-based row).</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="row">The row's position.</param>
    [GenerateComparisons(Comparison.SequenceEquals | Comparison.HasItem)]
    protected virtual IReadOnlyList<string>? GetRowTextsCore(IHtmlElement element, int row)
    {
        if (row < 0)
        {
            return [];
        }

        var cells = element.FindElements(Locator.ByCss($"tbody tr:nth-child({row + 1}) td"));
        return cells.Select(cell => cell.Text ?? string.Empty).ToList();
    }
}
