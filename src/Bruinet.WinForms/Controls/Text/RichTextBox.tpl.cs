using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Text;

/// <summary>
/// A rich text box: UI Automation shows it as a document whose text is its Value pattern, written
/// and confirmed as a text box's is. RichEdit reads its text back with a closing line end, which is
/// not part of what was written, so the text is read without it.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class RichTextBox<TScope> : TextBox<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public RichTextBox(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public RichTextBox(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    /// <summary>The text without RichEdit's closing line end.</summary>
    /// <param name="value">The Value pattern's text.</param>
    protected override string? ShownText(string? value) => value?.TrimEnd('\r', '\n');

    /// <summary>The text without RichEdit's closing line end.</summary>
    /// <param name="element">The pre-found element.</param>
    protected override string? GetTextCore(IWinFormsElement element) => ShownText(element.Text);
}
