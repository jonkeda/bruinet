using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Text;

/// <summary>
/// A password field (<c>PasswordBox</c>). UI Automation lets a password be written through the
/// Value pattern and never read back, so its text is written once and not confirmed: the page's
/// response (an error shown, a button enabled) is what a test asserts.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class PasswordBox<TScope> : TextBox<TScope>
    where TScope : IWpfScope<TScope>
{
    public PasswordBox(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public PasswordBox(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    /// <summary>Writes the password once; it cannot be read back to confirm it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The password; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void SetTextCore(IWpfElement element, string? text, int? timeoutMs = null)
    {
        if (text != null)
            element.SetValue(text);
    }

    /// <summary>Empties the password once; it cannot be read back to confirm it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void ClearCore(IWpfElement element, int? timeoutMs = null)
    {
        element.SetValue(string.Empty);
    }

    /// <summary>A password cannot be read, so it cannot be appended to.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <exception cref="NotSupportedException">Always, when <paramref name="text"/> is not null.</exception>
    protected override void AppendCore(IWpfElement element, string? text, int? timeoutMs = null)
    {
        if (text != null)
            throw new NotSupportedException($"{Locator} is a password field: its text cannot be read, so it cannot be appended to. Use SetText.");
    }
}
