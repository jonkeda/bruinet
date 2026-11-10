using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Controls;
using Bruinet.Html.Interfaces;

namespace Bruinet.Blazor.Controls;

/// <summary>
/// An &lt;iframe&gt; element, read and driven from outside through script. To use controls inside
/// a same-origin frame, use a <c>FrameScope</c> instead.
/// </summary>
public partial class IFrameControl<TScope> : ViewBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    public IFrameControl(IHtmlScope<TScope> scope, Locator locator) : base(scope, locator) { }
    public IFrameControl(IHtmlScope<TScope> scope, string selectorOrId) : base(scope, selectorOrId) { }

    /// <summary>The frame's <c>src</c>.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains)]
    protected virtual string? GetSourceCore(IHtmlElement element) => element.GetDomAttribute("src");

    /// <summary>The frame's <c>title</c>.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetTitleCore(IHtmlElement element) => element.GetDomAttribute("title");

    /// <summary>The frame's <c>name</c>.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetNameCore(IHtmlElement element) => element.GetDomAttribute("name");

    /// <summary>Clicks the element matching <paramref name="selector"/> inside the frame.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="selector">A CSS selector inside the frame's document.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClickInsideCore(IHtmlElement element, string selector, int? timeoutMs = null)
        => element.Evaluate($"(iframe) => iframe.contentDocument.querySelector('{EscapeSelector(selector)}').click()");

    /// <summary>
    /// Sets the value of the input matching <paramref name="selector"/> inside the frame, raising
    /// its input event, and confirms it took.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="selector">A CSS selector inside the frame's document.</param>
    /// <param name="text">The value to set.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void FillInsideCore(IHtmlElement element, string selector, string? text, int? timeoutMs = null)
        => ActAndConfirm(
            "FillInside",
            () => element.Evaluate($"(iframe) => {{ const el = iframe.contentDocument.querySelector('{EscapeSelector(selector)}'); el.value = '{EscapeJsString(text)}'; el.dispatchEvent(new Event('input', {{ bubbles: true }})); }}"),
            () => element.Evaluate<string?>($"(iframe) => iframe.contentDocument.querySelector('{EscapeSelector(selector)}').value"),
            value => value == (text ?? string.Empty),
            $"'{text}' in '{selector}'", timeoutMs);

    /// <summary>The text of the element matching <paramref name="selector"/> inside the frame; null when there is none.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="selector">A CSS selector inside the frame's document.</param>
    protected virtual string? GetTextInsideCore(IHtmlElement element, string selector)
        => element.Evaluate<string?>($"(iframe) => {{ const el = iframe.contentDocument.querySelector('{EscapeSelector(selector)}'); return el ? el.textContent : null; }}");

    /// <summary>Whether an element matches <paramref name="selector"/> inside the frame.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="selector">A CSS selector inside the frame's document.</param>
    protected virtual bool? IsElementInsideCore(IHtmlElement? element, string selector)
        => element?.Evaluate<bool>($"(iframe) => iframe.contentDocument.querySelector('{EscapeSelector(selector)}') !== null");

    private static string EscapeSelector(string selector) => selector.Replace("'", "\\'");
    private static string EscapeJsString(string? value) => value?.Replace("\\", "\\\\").Replace("'", "\\'") ?? "";
}
