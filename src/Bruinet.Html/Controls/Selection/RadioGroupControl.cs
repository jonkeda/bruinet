using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Selection;

/// <summary>
/// A group of radio buttons, read and driven as one selector. The element is the group's
/// container; each option is an &lt;input type="radio"&gt; with a &lt;label&gt;.
/// </summary>
public class RadioGroupControl<TScope> : SelectorControlBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    public RadioGroupControl(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public RadioGroupControl(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    private static readonly Locator Radios = Locator.ByCss("input[type='radio']");

    private static readonly Locator Labels = Locator.ByCss("label");

    /// <inheritdoc />
    protected override void SelectByValueCore(IHtmlElement element, string? value, int? timeoutMs = null)
    {
        if (value == null)
            return;

        var radio = element.FindElement(Locator.ByCss($"input[type='radio'][value='{EscapeCssValue(value)}']"));
        Select(radio, $"'{value}'", timeoutMs);
    }

    /// <inheritdoc />
    protected override void SelectByTextCore(IHtmlElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        var radio = element.FindElements(Labels)
            .Where(label => string.Equals(label.Text?.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(label => RadioOf(element, label))
            .FirstOrDefault(candidate => candidate != null)
            ?? throw new InvalidOperationException($"Radio option with label '{text}' was not found. Locator: {Locator}");
        Select(radio, $"'{text}'", timeoutMs);
    }

    /// <inheritdoc />
    protected override void SelectByIndexCore(IHtmlElement element, int? index, int? timeoutMs = null)
    {
        if (index == null)
            return;

        var radios = element.FindElements(Radios);
        if (index < 0 || index >= radios.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Radio index {index} is out of range (0-{radios.Count - 1}). Locator: {Locator}");
        }

        Select(radios[index.Value], $"option {index}", timeoutMs);
    }

    /// <inheritdoc />
    protected override string? GetSelectedValueCore(IHtmlElement? element)
        => element?.TryFindElement(Locator.ByCss("input[type='radio']:checked"))?.GetAttribute("value");

    /// <inheritdoc />
    protected override string? GetSelectedTextCore(IHtmlElement? element)
    {
        if (element == null)
            return null;

        return element.FindElements(Labels)
            .FirstOrDefault(label => RadioOf(element, label)?.IsChecked == true)
            ?.Text?.Trim();
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string>? GetItemTextsCore(IHtmlElement? element)
        => element?.FindElements(Labels).Select(label => label.Text?.Trim() ?? string.Empty).ToList();

    private void Select(IHtmlElement radio, string what, int? timeoutMs)
        => ActAndConfirm(
            "Click", radio.Click,
            () => radio.IsChecked, isChecked => isChecked,
            $"{what} selected", timeoutMs);

    // A radio is inside its label, or named by the label's "for".
    private static IHtmlElement? RadioOf(IHtmlElement group, IHtmlElement label)
    {
        if (label.TryFindElement(Radios) is { } nested)
        {
            return nested;
        }

        var forId = label.GetAttribute("for");
        return string.IsNullOrWhiteSpace(forId)
            ? null
            : group.TryFindElement(Locator.ByCss($"input[type='radio']#{EscapeCssId(forId)}"));
    }

    private static string EscapeCssValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private static string EscapeCssId(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(".", "\\.")
            .Replace("#", "\\#")
            .Replace(":", "\\:");
    }
}
