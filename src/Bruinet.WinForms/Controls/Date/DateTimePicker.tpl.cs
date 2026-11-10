using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Date;

/// <summary>
/// A date picker (<c>DateTimePicker</c>, short format). UI Automation shows its date as the text
/// of its Value pattern, and ignores a write to it, so a date is typed into its fields with the
/// keyboard, through <c>PhysicalInput</c> (AD-005), and confirmed by reading the text back.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class DateTimePicker<TScope> : ClickableControlBase<TScope>, IDateControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public DateTimePicker(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public DateTimePicker(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Core Methods

    /// <summary>The date shown, read in the current culture; null when it does not read as one.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual DateTime? GetDateCore(IWinFormsElement? element)
        => DateTime.TryParse(element?.Value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date) ? date.Date : null;

    /// <summary>
    /// Types <paramref name="date"/> into the picker's fields, in the order the culture's short date
    /// pattern gives them, and confirms it shows that date. The day is typed as 1 first and set
    /// last, so a day the old month does not have is not cut short. No Enter: it would press the
    /// form's accept button.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="date">The date; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetDateCore(IWinFormsElement element, DateTime? date, int? timeoutMs = null)
    {
        if (date is not { } target || GetDateCore(element) == target.Date)
            return;

        var fields = Fields(CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern);
        ActAndConfirm(
            "SetDate",
            () =>
            {
                TypeFields(element, fields, field => field.Kind == 'd' ? "1" : field.Text(target));
                TypeFields(element, fields, field => field.Text(target));
            },
            () => GetDateCore(element), shown => shown == target.Date,
            target.ToString("d", CultureInfo.CurrentCulture), timeoutMs);
    }

    #endregion

    private static void TypeFields(IWinFormsElement element, IReadOnlyList<Field> fields, Func<Field, string> text)
    {
        element.PressKey("HOME");
        for (var i = 0; i < fields.Count; i++)
        {
            foreach (var character in text(fields[i]))
            {
                element.PressKey(character.ToString());
            }

            if (i < fields.Count - 1)
            {
                element.PressKey("RIGHT");
            }
        }
    }

    // The day, month and year fields of a short date pattern ("dd-MMM-yy", "M/d/yyyy"), in order.
    private static List<Field> Fields(string pattern)
    {
        var fields = new List<Field>();
        for (var i = 0; i < pattern.Length; i++)
        {
            var kind = pattern[i];
            if (kind is not ('d' or 'M' or 'y'))
                continue;

            var width = 1;
            while (i + width < pattern.Length && pattern[i + width] == kind)
                width++;

            fields.Add(new Field(kind, width));
            i += width - 1;
        }

        return fields;
    }

    private readonly record struct Field(char Kind, int Width)
    {
        // A month field takes its number even when it shows a name; a two-digit year its last two digits.
        public string Text(DateTime date) => Kind switch
        {
            'd' => date.Day.ToString(CultureInfo.InvariantCulture),
            'M' => date.Month.ToString(CultureInfo.InvariantCulture),
            _ => Width <= 2
                ? (date.Year % 100).ToString("00", CultureInfo.InvariantCulture)
                : date.Year.ToString(CultureInfo.InvariantCulture),
        };
    }
}
