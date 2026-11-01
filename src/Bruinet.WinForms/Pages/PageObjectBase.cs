using Bruinet.WinForms.Controls;

namespace Bruinet.WinForms.Pages;

/// <summary>
/// Base class for WinForms page objects with fluent method chaining support.
/// Uses CRTP (Curiously Recurring Template Pattern) for strongly-typed fluent returns.
/// </summary>
/// <typeparam name="TSelf">The concrete page type (CRTP pattern).</typeparam>
public abstract class PageObjectBase<TSelf> : ObjectBase, IWinFormsPage<TSelf>
    where TSelf : PageObjectBase<TSelf>
{
    private readonly IWinFormsTestContext _context;

    /// <summary>
    /// Creates a new page object with the specified context.
    /// </summary>
    protected PageObjectBase(IWinFormsTestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public override IWinFormsTestContext Context => _context;

    /// <summary>
    /// Gets this page as the typed page reference (for fluent chaining).
    /// </summary>
    public TSelf Self => (TSelf)this;

    #region IPageObject Implementation

    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => _context.DefaultLocatorStrategy;

    /// <inheritdoc />
    public virtual bool IsLoaded(int? timeoutMs = null)
    {
        return _context.TryFindElement(Locator.ByAutomationId(Name)) != null;
    }

    /// <inheritdoc />
    public bool WaitLoaded(bool? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? _context.Timeouts.PageLoad;
        return Poll(() => IsLoaded() == expected.Value, timeout);
    }

    /// <inheritdoc />
    public void AssertLoaded(bool? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return;
        if (!WaitLoaded(expected, timeoutMs))
        {
            var actual = IsLoaded();
            throw new PageLoadException(
                message ?? $"Expected page '{Name}' {(expected.Value ? "to be loaded" : "not to be loaded")} but loaded state is {actual}.");
        }
    }

    /// <inheritdoc />
    public virtual string? GetTitle(int? timeoutMs = null)
    {
        return Name;
    }

    /// <inheritdoc />
    public bool WaitTitle(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? _context.Timeouts.DefaultWait;
        return Poll(() => GetTitle() == expected, timeout);
    }

    /// <inheritdoc />
    public void AssertTitle(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return;
        if (!WaitTitle(expected, timeoutMs))
        {
            var actual = GetTitle();
            throw new PageLoadException(
                message ?? $"Expected page title '{expected}' but got '{actual ?? "(null)"}'.");
        }
    }

    /// <inheritdoc />
    public void TakeScreenshot(string? filename = null, int? timeoutMs = null)
    {
        var path = filename ?? $"{Name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        _context.SaveScreenshot(path);
    }

    #endregion

    #region IWinFormsElementScope Implementation

    /// <inheritdoc />
    public IPageObject? Page => this;

    /// <inheritdoc />
    public bool IsReady(int? timeoutMs = null) => IsLoaded(timeoutMs);

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null) => WaitLoaded(true, timeoutMs);

    /// <inheritdoc />
    IWinFormsElement? IElementScope<IWinFormsElement>.TryFindElement(Locator locator)
    {
        return _context.TryFindElement(locator);
    }

    /// <inheritdoc />
    IWinFormsElement IElementScope<IWinFormsElement>.FindElement(Locator locator)
    {
        return _context.FindElement(locator);
    }

    /// <inheritdoc />
    IReadOnlyList<IWinFormsElement> IElementScope<IWinFormsElement>.FindElements(Locator locator)
    {
        return _context.FindElements(locator);
    }

    #endregion

    #region Factory Methods

    /// <summary>Creates a Button control within this page scope.</summary>
    protected Button<TSelf> Button(Locator locator) => new(this, locator);

    /// <summary>Creates a Button control using the scope default locator.</summary>
    protected Button<TSelf> Button(string locator) => new(this, locator);

    /// <summary>Creates a CheckBox control within this page scope.</summary>
    protected CheckBox<TSelf> CheckBox(Locator locator) => new(this, locator);

    /// <summary>Creates a CheckBox control using the scope default locator.</summary>
    protected CheckBox<TSelf> CheckBox(string locator) => new(this, locator);

    /// <summary>Creates a ComboBox control within this page scope.</summary>
    protected ComboBox<TSelf> ComboBox(Locator locator) => new(this, locator);

    /// <summary>Creates a ComboBox control using the scope default locator.</summary>
    protected ComboBox<TSelf> ComboBox(string locator) => new(this, locator);

    /// <summary>Creates a DataGridView control within this page scope.</summary>
    protected DataGridView<TSelf> DataGridView(Locator locator) => new(this, locator);

    /// <summary>Creates a DataGridView control using the scope default locator.</summary>
    protected DataGridView<TSelf> DataGridView(string locator) => new(this, locator);

    /// <summary>Creates a DateTimePicker control within this page scope.</summary>
    protected DateTimePicker<TSelf> DateTimePicker(Locator locator) => new(this, locator);

    /// <summary>Creates a DateTimePicker control using the scope default locator.</summary>
    protected DateTimePicker<TSelf> DateTimePicker(string locator) => new(this, locator);

    /// <summary>Creates a GroupBox control within this page scope.</summary>
    protected GroupBox<TSelf> GroupBox(Locator locator) => new(this, locator);

    /// <summary>Creates a GroupBox control using the scope default locator.</summary>
    protected GroupBox<TSelf> GroupBox(string locator) => new(this, locator);

    /// <summary>Creates a Label control within this page scope.</summary>
    protected Label<TSelf> Label(Locator locator) => new(this, locator);

    /// <summary>Creates a Label control using the scope default locator.</summary>
    protected Label<TSelf> Label(string locator) => new(this, locator);

    /// <summary>Creates a ListBox control within this page scope.</summary>
    protected ListBox<TSelf> ListBox(Locator locator) => new(this, locator);

    /// <summary>Creates a ListBox control using the scope default locator.</summary>
    protected ListBox<TSelf> ListBox(string locator) => new(this, locator);

    /// <summary>Creates a NumericUpDown control within this page scope.</summary>
    protected NumericUpDown<TSelf> NumericUpDown(Locator locator) => new(this, locator);

    /// <summary>Creates a NumericUpDown control using the scope default locator.</summary>
    protected NumericUpDown<TSelf> NumericUpDown(string locator) => new(this, locator);

    /// <summary>Creates a PasswordBox control within this page scope.</summary>
    protected PasswordBox<TSelf> PasswordBox(Locator locator) => new(this, locator);

    /// <summary>Creates a PasswordBox control using the scope default locator.</summary>
    protected PasswordBox<TSelf> PasswordBox(string locator) => new(this, locator);

    /// <summary>Creates a ProgressBar control within this page scope.</summary>
    protected ProgressBar<TSelf> ProgressBar(Locator locator) => new(this, locator);

    /// <summary>Creates a ProgressBar control using the scope default locator.</summary>
    protected ProgressBar<TSelf> ProgressBar(string locator) => new(this, locator);

    /// <summary>Creates a RadioButton control within this page scope.</summary>
    protected RadioButton<TSelf> RadioButton(Locator locator) => new(this, locator);

    /// <summary>Creates a RadioButton control using the scope default locator.</summary>
    protected RadioButton<TSelf> RadioButton(string locator) => new(this, locator);

    /// <summary>Creates a RichTextBox control within this page scope.</summary>
    protected RichTextBox<TSelf> RichTextBox(Locator locator) => new(this, locator);

    /// <summary>Creates a RichTextBox control using the scope default locator.</summary>
    protected RichTextBox<TSelf> RichTextBox(string locator) => new(this, locator);

    /// <summary>Creates a TabControl control within this page scope.</summary>
    protected TabControl<TSelf> TabControl(Locator locator) => new(this, locator);

    /// <summary>Creates a TabControl control using the scope default locator.</summary>
    protected TabControl<TSelf> TabControl(string locator) => new(this, locator);

    /// <summary>Creates a TextBox control within this page scope.</summary>
    protected TextBox<TSelf> TextBox(Locator locator) => new(this, locator);

    /// <summary>Creates a TextBox control using the scope default locator.</summary>
    protected TextBox<TSelf> TextBox(string locator) => new(this, locator);

    /// <summary>Creates a TrackBar control within this page scope.</summary>
    protected TrackBar<TSelf> TrackBar(Locator locator) => new(this, locator);

    /// <summary>Creates a TrackBar control using the scope default locator.</summary>
    protected TrackBar<TSelf> TrackBar(string locator) => new(this, locator);

    #endregion
}
