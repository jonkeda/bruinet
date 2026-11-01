using Bruinet.Core;

namespace Bruinet.Maui.Controls.Text;

/// <summary>
/// MAUI Editor control for multi-line text input.
/// Inherits all text manipulation from Entry.
/// Includes FlaUI-specific clear handling for Windows.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Editor<TScope> : Entry<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new editor control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the editor element.</param>
    public Editor(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new editor control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public Editor(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Clear Override for FlaUI

    /// <summary>
    /// Core implementation of Clear using pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout for enabled check.</param>
    protected override void ClearCore(IMauiElement element, int? timeoutMs = null)
    {
        element.Clear();
    }

    /// <summary>
    /// Core implementation of SetText using pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text to set.</param>
    /// <param name="timeoutMs">Optional timeout for enabled check.</param>
    protected override void SetTextCore(IMauiElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        element.Clear();
        element.SendKeys(text, TextInputMethod.SetValue);
    }

    #endregion
}
