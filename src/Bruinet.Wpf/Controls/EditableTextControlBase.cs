namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for editable text WPF controls (TextBox, PasswordBox).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class EditableTextControlBase<TScope> : ControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    /// <summary>
    /// Creates a new editable text control with the specified scope and locator.
    /// </summary>
    protected EditableTextControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator) { }

    /// <summary>
    /// Creates a new editable text control using the scope's default locator strategy.
    /// </summary>
    protected EditableTextControlBase(IWpfScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue) { }

    /// <summary>
    /// Enters text into the control (clears first, then types).
    /// </summary>
    public virtual TScope Enter(string? text, int? timeoutMs = null)
    {
        if (text == null)
            return ContainingScope;
        return Run("Enter", e =>
        {
            e.Clear();
            e.SendKeys(text);
        }, value: text, timeoutMs: timeoutMs);
    }

    /// <summary>
    /// Clears the control text.
    /// </summary>
    public virtual TScope Clear(int? timeoutMs = null)
    {
        return Run("Clear", e => e.Clear(), timeoutMs: timeoutMs);
    }

    /// <summary>
    /// Sets the text using the Value pattern (bypassing keyboard).
    /// </summary>
    public virtual TScope SetText(string? text, int? timeoutMs = null)
    {
        if (text == null)
            return ContainingScope;
        return Run("SetText", e =>
        {
            e.SendKeys(text, TextInputMethod.SetValue);
        }, value: text, timeoutMs: timeoutMs);
    }

    /// <summary>
    /// Appends text without clearing first.
    /// </summary>
    public virtual TScope Append(string? text, int? timeoutMs = null)
    {
        if (text == null)
            return ContainingScope;
        return Run("Append", e =>
        {
            e.SendKeys(text);
        }, value: text, timeoutMs: timeoutMs);
    }
}
