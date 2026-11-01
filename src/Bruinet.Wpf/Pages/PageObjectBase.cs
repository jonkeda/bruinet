using Bruinet.Wpf.Controls;

namespace Bruinet.Wpf.Pages;

/// <summary>
/// Base class for WPF page objects with fluent method chaining support.
/// Uses CRTP (Curiously Recurring Template Pattern) for strongly-typed fluent returns.
/// </summary>
/// <typeparam name="TSelf">The concrete page type (CRTP pattern).</typeparam>
public abstract class PageObjectBase<TSelf> : ObjectBase, IWpfPage<TSelf>
    where TSelf : PageObjectBase<TSelf>
{
    private readonly IWpfTestContext _context;

    /// <summary>
    /// Creates a new page object with the specified context.
    /// </summary>
    protected PageObjectBase(IWpfTestContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public override IWpfTestContext Context => _context;

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

    #region IWpfElementScope Implementation

    /// <inheritdoc />
    public IPageObject? Page => this;

    /// <inheritdoc />
    public bool IsReady(int? timeoutMs = null) => IsLoaded(timeoutMs);

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null) => WaitLoaded(true, timeoutMs);

    /// <inheritdoc />
    IWpfElement? IElementScope<IWpfElement>.TryFindElement(Locator locator)
    {
        return _context.TryFindElement(locator);
    }

    /// <inheritdoc />
    IWpfElement IElementScope<IWpfElement>.FindElement(Locator locator)
    {
        return _context.FindElement(locator);
    }

    /// <inheritdoc />
    IReadOnlyList<IWpfElement> IElementScope<IWpfElement>.FindElements(Locator locator)
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

    /// <summary>Creates a Label control within this page scope.</summary>
    protected Label<TSelf> Label(Locator locator) => new(this, locator);

    /// <summary>Creates a Label control using the scope default locator.</summary>
    protected Label<TSelf> Label(string locator) => new(this, locator);

    /// <summary>Creates a ListBox control within this page scope.</summary>
    protected ListBox<TSelf> ListBox(Locator locator) => new(this, locator);

    /// <summary>Creates a ListBox control using the scope default locator.</summary>
    protected ListBox<TSelf> ListBox(string locator) => new(this, locator);

    /// <summary>Creates a PasswordBox control within this page scope.</summary>
    protected PasswordBox<TSelf> PasswordBox(Locator locator) => new(this, locator);

    /// <summary>Creates a PasswordBox control using the scope default locator.</summary>
    protected PasswordBox<TSelf> PasswordBox(string locator) => new(this, locator);

    /// <summary>Creates a ProgressBar control within this page scope.</summary>
    protected ProgressBar<TSelf> ProgressBar(Locator locator) => new(this, locator);

    /// <summary>Creates a ProgressBar control using the scope default locator.</summary>
    protected ProgressBar<TSelf> ProgressBar(string locator) => new(this, locator);

    /// <summary>Creates a ScrollView control within this page scope.</summary>
    protected ScrollView<TSelf> ScrollView(Locator locator) => new(this, locator);

    /// <summary>Creates a ScrollView control using the scope default locator.</summary>
    protected ScrollView<TSelf> ScrollView(string locator) => new(this, locator);

    /// <summary>Creates a Slider control within this page scope.</summary>
    protected Slider<TSelf> Slider(Locator locator) => new(this, locator);

    /// <summary>Creates a Slider control using the scope default locator.</summary>
    protected Slider<TSelf> Slider(string locator) => new(this, locator);

    /// <summary>Creates a TabItem control within this page scope.</summary>
    protected TabItem<TSelf> TabItem(Locator locator) => new(this, locator);

    /// <summary>Creates a TabItem control using the scope default locator.</summary>
    protected TabItem<TSelf> TabItem(string locator) => new(this, locator);

    /// <summary>Creates a TextBox control within this page scope.</summary>
    protected TextBox<TSelf> TextBox(Locator locator) => new(this, locator);

    /// <summary>Creates a TextBox control using the scope default locator.</summary>
    protected TextBox<TSelf> TextBox(string locator) => new(this, locator);

    /// <summary>Creates a TreeView control within this page scope.</summary>
    protected TreeView<TSelf> TreeView(Locator locator) => new(this, locator);

    /// <summary>Creates a TreeView control using the scope default locator.</summary>
    protected TreeView<TSelf> TreeView(string locator) => new(this, locator);

    #endregion
}
