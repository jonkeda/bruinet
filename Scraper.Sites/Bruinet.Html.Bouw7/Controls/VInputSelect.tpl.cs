// <scraped control="div||v-input__control|div" variant="selector" root="lifted" from="2026-10-02-recording/008-department.nodes.jsonl#476" />
using Bruinet.Core.Locators;
using Bruinet.Html.Controls;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Bouw7.Controls;

public partial class VInputSelect<TScope> : SelectorControlBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    private static readonly Locator Slot = Locator.ByCss("div.v-input__slot");
    private static readonly Locator Input = Locator.ByCss("input[type=text]");
    private static readonly Locator SelectionText = Locator.ByCss("div.v-select__selection");
    private static readonly Locator SelectionsText = Locator.ByCss("div.v-select__selections");
    private static readonly Locator LegendText = Locator.ByCss("legend");
    private static readonly Locator SubtitleText = Locator.ByCss("span.subtitle-2");
    private static readonly Locator Options = Locator.ByCss("[role='option']");

    public VInputSelect(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public VInputSelect(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    protected virtual void OpenCore(IHtmlElement element, int? timeoutMs = null)
    {
        var slot = element.TryFindElement(Slot)
            ?? throw new InvalidOperationException($"Select slot was not found. Locator: {Locator}");

        ActAndConfirm(
            "Open",
            () => slot.Click(),
            () => slot.GetAttribute("aria-expanded"),
            expanded => string.Equals(expanded, "true", StringComparison.OrdinalIgnoreCase),
            "expanded",
            timeoutMs);
    }

    protected virtual void SelectItemCore(IHtmlElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        OpenCore(element, timeoutMs);

        var item = FindOption(element, text)
            ?? throw new InvalidOperationException($"Select option with text '{text}' was not found. Locator: {Locator}");

        ActAndConfirm(
            "SelectItem",
            () => item.Click(),
            () => GetSelectedTextCore(element),
            selected => string.Equals(selected?.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase),
            $"'{text}' selected",
            timeoutMs);
    }

    protected override void SelectByTextCore(IHtmlElement element, string? text, int? timeoutMs = null)
        => SelectItemCore(element, text, timeoutMs);

    protected override string? GetSelectedTextCore(IHtmlElement? element)
    {
        if (element == null)
            return null;

        var input = element.TryFindElement(Input);
        var inputValue = input?.InputValue?.Trim();
        if (!string.IsNullOrEmpty(inputValue))
        {
            return inputValue;
        }

        return GetTextFrom(element, SelectionText)
            ?? GetTextFrom(element, SelectionsText)
            ?? GetTextFrom(element, LegendText)
            ?? GetTextFrom(element, SubtitleText);
    }

    private IHtmlElement? FindOption(IHtmlElement element, string text)
    {
        var slot = element.TryFindElement(Slot);
        var ownsId = slot?.GetAttribute("aria-owns");

        if (!string.IsNullOrWhiteSpace(ownsId))
        {
            var ownedOption = FindOptionByText(element.FindElements(Locator.ByCss($"#{ownsId} [role='option']")), text);
            if (ownedOption != null)
            {
                return ownedOption;
            }
        }

        return FindOptionByText(element.FindElements(Options), text);
    }

    private static IHtmlElement? FindOptionByText(IEnumerable<IHtmlElement> options, string text)
        => options.FirstOrDefault(option => string.Equals(option.Text?.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string? GetTextFrom(IHtmlElement element, Locator locator)
        => element.TryFindElement(locator)?.Text?.Trim();
}
