namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for editable text WinForms controls (TextBox, PasswordBox, RichTextBox).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class EditableTextControlBase<TScope> : ControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    protected EditableTextControlBase(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator) { }

    protected EditableTextControlBase(IWinFormsScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue) { }

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

    public virtual TScope Clear(int? timeoutMs = null)
    {
        return Run("Clear", e => e.Clear(), timeoutMs: timeoutMs);
    }

    public virtual TScope SetText(string? text, int? timeoutMs = null)
    {
        if (text == null)
            return ContainingScope;
        return Run("SetText", e =>
        {
            e.SendKeys(text, TextInputMethod.SetValue);
        }, value: text, timeoutMs: timeoutMs);
    }

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
