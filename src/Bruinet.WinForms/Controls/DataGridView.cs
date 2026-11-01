using Bruinet.WinForms.FlaUI;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Bruinet.WinForms.Controls;

/// <summary>
/// WinForms DataGridView control with grid-specific operations.
/// </summary>
public sealed class DataGridView<TScope> : ControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public DataGridView(IWinFormsScope<TScope> scope, Locator locator) : base(scope, locator) { }
    public DataGridView(IWinFormsScope<TScope> scope, string locatorValue) : base(scope, locatorValue) { }

    /// <summary>Gets the number of rows in the grid.</summary>
    public int? GetRowCount(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is FlaUIWinFormsElement flaui)
            {
                var grid = flaui.Element.AsGrid();
                return grid.RowCount;
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>Gets the number of columns in the grid.</summary>
    public int? GetColumnCount(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is FlaUIWinFormsElement flaui)
            {
                var grid = flaui.Element.AsGrid();
                return grid.ColumnCount;
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>Gets the value of a specific cell.</summary>
    public string? GetCellValue(int row, int column, int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is FlaUIWinFormsElement flaui)
            {
                var grid = flaui.Element.AsGrid();
                var rows = grid.Rows;
                if (row >= rows.Length)
                    return null;
                var cells = rows[row].Cells;
                if (column >= cells.Length)
                    return null;
                return cells[column].Value;
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>Selects a row by index.</summary>
    public TScope SelectRow(int row, int? timeoutMs = null)
    {
        RunWithElement(e =>
        {
            if (e is FlaUIWinFormsElement flaui)
            {
                var grid = flaui.Element.AsGrid();
                var rows = grid.Rows;
                if (row < rows.Length)
                {
                    rows[row].Click();
                }
            }
        }, timeoutMs);
        return ContainingScope;
    }

    /// <summary>Gets column header texts.</summary>
    public IReadOnlyList<string>? GetColumnHeaders(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is FlaUIWinFormsElement flaui)
            {
                var grid = flaui.Element.AsGrid();
                return grid.Header?.Columns?.Select(c => c.Text ?? "").ToList();
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>Clicks a specific cell.</summary>
    public TScope ClickCell(int row, int column, int? timeoutMs = null)
    {
        RunWithElement(e =>
        {
            if (e is FlaUIWinFormsElement flaui)
            {
                var grid = flaui.Element.AsGrid();
                var rows = grid.Rows;
                if (row < rows.Length)
                {
                    var cells = rows[row].Cells;
                    if (column < cells.Length)
                        cells[column].Click();
                }
            }
        }, timeoutMs);
        return ContainingScope;
    }

    /// <summary>Waits for the grid to have the expected row count.</summary>
    public bool WaitRowCount(int? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetRowCount() == expected, timeout);
    }

    /// <summary>Asserts the grid has the expected row count.</summary>
    public TScope AssertRowCount(int? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitRowCount(expected, timeoutMs))
        {
            var actual = GetRowCount();
            throw new AssertionException(
                message ?? $"Expected row count {expected} for '{AutomationId}' but got {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }
}
