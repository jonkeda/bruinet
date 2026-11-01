namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// Page object for the TextTestView. Exposes all text input controls (Entry, Editor, SearchBar).
/// Demonstrates the page object pattern with control locators and action methods.
/// </summary>
public class TextTestPage : PageObjectBase<TextTestPage>
{
    public TextTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "TextTestPage";

    #region Entry Controls

    /// <summary>
    /// The Entry test control for single-line text input.
    /// </summary>
    public Entry<TextTestPage> TestEntry => new(this, "TestEntry");

    /// <summary>An entry the app marked read-only.</summary>
    public Entry<TextTestPage> ReadOnlyEntry => new(this, "ReadOnlyEntry");

    /// <summary>
    /// The Editor test control for multi-line text input.
    /// </summary>
    public Editor<TextTestPage> TestEditor => new(this, "TestEditor");

    /// <summary>
    /// The SearchBar test control for search input.
    /// </summary>
    public SearchBar<TextTestPage> TestSearchBar => new(this, "TestSearchBar");

    #endregion

    #region Buttons

    /// <summary>
    /// The Clear Entry button.
    /// </summary>
    public Button<TextTestPage> ClearEntryButton => new(this, "ClearEntryButton");

    /// <summary>
    /// The Clear Editor button.
    /// </summary>
    public Button<TextTestPage> ClearEditorButton => new(this, "ClearEditorButton");

    /// <summary>
    /// The Clear Search button.
    /// </summary>
    public Button<TextTestPage> ClearSearchButton => new(this, "ClearSearchButton");

    /// <summary>
    /// The Reset All button.
    /// </summary>
    public Button<TextTestPage> ResetAllButton => new(this, "ResetAllButton");

    #endregion

    #region Labels

    /// <summary>
    /// The Entry status message label.
    /// </summary>
    public Label<TextTestPage> EntryStatusLabel => new(this, "EntryStatusLabel");

    /// <summary>
    /// The Editor status message label.
    /// </summary>
    public Label<TextTestPage> EditorStatusLabel => new(this, "EditorStatusLabel");

    /// <summary>
    /// The Search status message label.
    /// </summary>
    public Label<TextTestPage> SearchStatusLabel => new(this, "SearchStatusLabel");

    #endregion
}
