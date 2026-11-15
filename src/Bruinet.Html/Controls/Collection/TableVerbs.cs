using Bruinet.Html.Interfaces;
using Bruinet.Bdd;

namespace Bruinet.Html.Controls.Collection;

/// <summary>
/// The steps every table takes, written once (<see cref="BddVerbsAttribute"/>): a table control, a UI kit's
/// (<c>VDataTable</c>) or a suite's own (<c>HeimdallTable</c>) gets them by deriving from <see cref="TableControl{TScope}"/>.
/// Each waits through the table's own assertions. In C# they are extension methods: <c>page.Contacts.ShowsOnly(["Arend de Krom"])</c>.
/// </summary>
[BddVerbs]
public static class TableVerbs
{
    /// <summary>The table's headers, in order from the first column.</summary>
    /// <param name="table">The table.</param>
    /// <param name="columns">The headers' texts.</param>
    /// <typeparam name="TScope">The table's scope.</typeparam>
    [BddThen("{control} has the columns {columns}")]
    public static void HasColumns<TScope>(this TableControl<TScope> table, string[] columns)
        where TScope : IHtmlScope<TScope>
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columns);
        for (var column = 0; column < columns.Length; column++)
        {
            table.AssertHeaderText(column, columns[column]);
        }
    }

    /// <summary>The table's rows are these, one each, by their first cell, in order.</summary>
    /// <param name="table">The table.</param>
    /// <param name="values">The rows' first cells.</param>
    /// <typeparam name="TScope">The table's scope.</typeparam>
    [BddThen("{control} shows only {values}")]
    public static void ShowsOnly<TScope>(this TableControl<TScope> table, string[] values)
        where TScope : IHtmlScope<TScope>
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(values);
        table.AssertRowCount(values.Length);
        for (var row = 0; row < values.Length; row++)
        {
            table.AssertCellText(row, 0, values[row]);
        }
    }

    /// <summary>The table has no row.</summary>
    /// <param name="table">The table.</param>
    /// <typeparam name="TScope">The table's scope.</typeparam>
    [BddThen("{control} is empty")]
    public static void IsEmpty<TScope>(this TableControl<TScope> table)
        where TScope : IHtmlScope<TScope>
    {
        ArgumentNullException.ThrowIfNull(table);
        table.AssertRowCount(0);
    }

    /// <summary>The table has a row, waiting for rows that load after it.</summary>
    /// <param name="table">The table.</param>
    /// <typeparam name="TScope">The table's scope.</typeparam>
    [BddThen("{control} has rows")]
    public static void HasRows<TScope>(this TableControl<TScope> table)
        where TScope : IHtmlScope<TScope>
    {
        ArgumentNullException.ThrowIfNull(table);
        table.AssertHasRows();
    }

    /// <summary>Every row of the table has a cell that is <paramref name="value"/>.</summary>
    /// <param name="table">The table.</param>
    /// <param name="value">The cell's text.</param>
    /// <typeparam name="TScope">The table's scope.</typeparam>
    [BddThen("every row of {control} has {value}")]
    public static void EveryRowHas<TScope>(this TableControl<TScope> table, string value)
        where TScope : IHtmlScope<TScope>
    {
        ArgumentNullException.ThrowIfNull(table);
        table.AssertHasRows();
        var rows = table.GetRowCount() ?? 0;
        for (var row = 0; row < rows; row++)
        {
            table.AssertRowTextsHasItem(row, value);
        }
    }
}
