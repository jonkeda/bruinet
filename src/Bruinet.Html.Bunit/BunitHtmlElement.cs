using System.Drawing;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;
using IHtmlElement = Bruinet.Html.Interfaces.IHtmlElement;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Bruinet.Html.Bunit;

/// <summary>
/// One element of the rendered DOM, found again on every read as Playwright's locators are: a render
/// replaces bUnit's nodes, so a held node would go stale. Gone is <see cref="StaleElementException"/>,
/// which the call around it treats as a signal to look again.
/// </summary>
internal sealed class BunitHtmlElement : IHtmlElement
{
    private readonly BunitTestContext _context;
    private readonly Func<IElement?> _resolve;
    private readonly Locator? _locator;

    internal BunitHtmlElement(BunitTestContext context, Func<IElement?> resolve, Locator? locator)
    {
        _context = context;
        _resolve = resolve;
        _locator = locator;
    }

    private IElement Node => _resolve() ?? throw new StaleElementException(_locator);

    #region Identity

    // The path from the root by element index: the same element after a render keeps its key.
    public string InstanceKey => KeyOf(Node);

    #endregion

    #region State

    public bool Visible
    {
        get
        {
            var start = Node;
            if (start is IHtmlInputElement { Type: "hidden" })
            {
                return false;
            }

            for (var node = start; node is not null; node = node.ParentElement)
            {
                if (node.HasAttribute("hidden") || HidesItself(node.GetAttribute("style")) || IsBlazorErrorUi(node) || Folded(node)
                    || node is IHtmlDialogElement && !node.HasAttribute("open"))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public bool Enabled => !Disabled(Node);

    public bool Selected => Node is IHtmlOptionElement option ? option.IsSelected : Node.HasAttribute("selected");

    public string? Text => BunitLocators.Normalize(Node.TextContent);

    public string? TagName => Node.TagName.ToLowerInvariant();

    public Point Location => throw new BrowserOnlyException(nameof(Location));

    public Size Size => throw new BrowserOnlyException(nameof(Size));

    public Rectangle Rect => throw new BrowserOnlyException(nameof(Rect));

    public string? GetAttribute(string name) => Node.GetAttribute(name);

    public string? GetDomAttribute(string attributeName) => Node.GetAttribute(attributeName);

    public string? GetDomProperty(string propertyName) => propertyName switch
    {
        "value" => InputValue,
        "checked" => IsChecked ? "true" : "false",
        "disabled" => Enabled ? "false" : "true",
        "textContent" or "innerText" => Text,
        _ => Node.GetAttribute(propertyName),
    };

    public string? GetCssValue(string propertyName) => throw new BrowserOnlyException(nameof(GetCssValue));

    public string InnerHtml => Node.InnerHtml;

    public string OuterHtml => Node.OuterHtml;

    public bool IsChecked => Node is IHtmlInputElement input ? input.IsChecked || Node.HasAttribute("checked") : Node.HasAttribute("checked");

    // What the user entered, while no render has changed the value attribute since (BunitTestContext.EnteredValue); else the DOM's.
    public string InputValue
    {
        get
        {
            var node = Node;
            return _context.EnteredValue(KeyOf(node), node.GetAttribute("value")) ?? RenderedValue(node);
        }
    }

    #endregion

    #region Actions

    // A browser's click: the handler of the element or of the nearest one around it (the click bubbles); else a
    // submit button submits its form. A link follows its href after any handler, unless a handler on the way prevents
    // it, which Blazor's script does in a browser and nothing does here; a summary opens or closes its fold. A disabled control
    // gets no click, as in a browser.
    public void Click()
    {
        var node = Node;
        var handler = Handling(node, "onclick");
        if (Disabled(node) || handler is not null && Disabled(handler))
        {
            return;
        }

        // Read before the handler runs: its render may replace the link or move the fold.
        var prevented = PreventsDefault(node, "onclick");
        var href = node.Closest("a")?.GetAttribute("href") is { Length: > 0 } link && !link.StartsWith('#') && !prevented ? link : null;
        var fold = !prevented && FoldToggledBy(node) is { } details ? KeyOf(details) : null;

        // A browser checks a radio, or turns a check box over, and sends change when it is clicked: what @bind
        // and @onchange listen to. The click's own handler runs first, as in the browser.
        if (!prevented && node is IHtmlInputElement { Type: "radio" or "checkbox" } && Handles(node, "onchange"))
        {
            if (handler is not null)
            {
                Raise(handler, "onclick", new MouseEventArgs { Type = "click", Detail = 1 });
            }

            SetChecked(IsRadio(Node) || !IsChecked);
            return;
        }

        // bUnit itself submits the form after a submit button's own click handler.
        var target = handler ?? node;
        var form = SubmitsForm(target) ? target.Closest("form") : null;
        if (handler is not null)
        {
            Raise(handler, "onclick", new MouseEventArgs { Type = "click", Detail = 1 });
        }
        else if (form is not null)
        {
            Raise(form, "onsubmit", EventArgs.Empty);
            return;
        }

        if (fold is not null)
        {
            _context.Toggle(fold);
        }

        if (href is not null)
        {
            _context.Follow(href);
        }
    }

    public void ClickAt(double x, double y) => throw new BrowserOnlyException(nameof(ClickAt));

    // Keys: each character's key events where the element listens, then the value as Fill sets it.
    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys)
    {
        if (method == TextInputMethod.Keys)
        {
            foreach (var character in text)
            {
                var key = character.ToString();
                Raise(Node, "onkeydown", new KeyboardEventArgs { Key = key, Type = "keydown" });
                Raise(Node, "onkeyup", new KeyboardEventArgs { Key = key, Type = "keyup" });
            }
        }

        Fill(InputValue + text);
    }

    public void PressKey(string key)
    {
        Raise(Node, "onkeydown", new KeyboardEventArgs { Key = key, Code = key, Type = "keydown" });
        Raise(Node, "onkeyup", new KeyboardEventArgs { Key = key, Code = key, Type = "keyup" });
        if (key == "Enter" && Node is IHtmlInputElement && Node.Closest("form") is { } form)
        {
            Raise(form, "onsubmit", EventArgs.Empty);
        }
    }

    public void Clear() => Fill(string.Empty);

    public void DoubleClick() => Raise(Node, "ondblclick", new MouseEventArgs { Type = "dblclick", Detail = 2 });

    public void RightClick() => Raise(Node, "oncontextmenu", new MouseEventArgs { Type = "contextmenu", Button = 2 });

    public void Hover() => throw new BrowserOnlyException(nameof(Hover));

    public void LongPress(int durationMs = 1000) => throw new BrowserOnlyException(nameof(LongPress));

    // Nothing scrolls without layout; an element in the DOM is as reachable as it will be.
    public void ScrollIntoView(int timeoutMs)
    {
    }

    public void Swipe(int startX, int startY, int endX, int endY, int durationMs = 500) => throw new BrowserOnlyException(nameof(Swipe));

    public void Submit() => Raise(Node.Closest("form") ?? Node, "onsubmit", EventArgs.Empty);

    // What @bind sees: the input event where it listens to one, then the change event; an element that
    // listens to neither just holds the value.
    public void Fill(string value)
    {
        var node = Node;
        var input = Handles(node, "oninput");
        var change = Handles(node, "onchange");
        Entering(value, () =>
        {
            if (input)
            {
                Raise(node, "oninput", new ChangeEventArgs { Value = value });
            }

            if (change)
            {
                Raise(Node, "onchange", new ChangeEventArgs { Value = value });
            }
        });
    }

    public void SelectOption(string value) => Entering(value, () => Raise(Node, "onchange", new ChangeEventArgs { Value = value }));

    public void SelectOption(string[] values) => Raise(Node, "onchange", new ChangeEventArgs { Value = values });

    public void Check() => SetChecked(true);

    public void Uncheck() => SetChecked(false);

    public void Focus() => Raise(Node, "onfocus", new FocusEventArgs { Type = "focus" });

    public void Blur() => Raise(Node, "onblur", new FocusEventArgs { Type = "blur" });

    public void Evaluate(string expression) => throw new BrowserOnlyException(nameof(Evaluate));

    public T Evaluate<T>(string expression) => throw new BrowserOnlyException(nameof(Evaluate));

    #endregion

    #region Child lookup

    public IHtmlElement? TryFindElement(Locator locator) => FindElements(locator).FirstOrDefault();

    // An iframe is a document of its own, loaded by a browser; bUnit renders none.
    public IHtmlElement? TryGetFrameDocument() => throw new BrowserOnlyException(nameof(TryGetFrameDocument));

    public IReadOnlyList<IHtmlElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var node = Node;
        var count = BunitLocators.FindAll(css => node.QuerySelectorAll(css), locator).Count;
        var elements = new List<IHtmlElement>(count);
        for (var i = 0; i < count; i++)
        {
            var index = i;
            elements.Add(new BunitHtmlElement(_context, () => _resolve() is { } parent
                ? BunitLocators.FindAll(css => parent.QuerySelectorAll(css), locator).ElementAtOrDefault(index)
                : null, locator));
        }

        return elements;
    }

    #endregion

    private void SetChecked(bool value)
    {
        var node = Node;
        if (IsChecked == value || Disabled(node))
        {
            return;
        }

        // A checkbox's change carries whether it is checked; a radio's carries its value, "on" without one, as Blazor's script sends.
        if (Handles(node, "onchange"))
        {
            Raise(node, "onchange", new ChangeEventArgs { Value = IsRadio(node) ? node.GetAttribute("value") ?? "on" : value });
        }
        else
        {
            Raise(node, "onclick", new MouseEventArgs { Type = "click", Detail = 1 });
        }
    }

    // One event, where the element listens to it. A handler that throws is the app failing, as a
    // browser's circuit ends on it: the call ends at once rather than trying the event again.
    private void Raise(IElement node, string eventName, EventArgs args)
    {
        if (!Handles(node, eventName))
        {
            return;
        }

        _context.ForgetEntries();

        // The target is the element or one around it (its handler, its form): so many levels up, found again inside the renderer's
        // dispatcher, where no render can come between finding it and raising its event.
        var levels = LevelsUp(Node, node);
        try
        {
            // Raised as a browser does: returns when the handler awaits, a refusal seen here rather than by the renderer later.
            var rendersBefore = _context.RenderCount;
            var raised = _context.Dispatch(() =>
            {
                var target = levels is { } up ? Up(_resolve(), up) : node;
                if (target is null || !Handles(target, eventName))
                {
                    throw new StaleElementException(_locator, detail: $"Its {eventName} handler was rendered anew.");
                }

                return target.TriggerEventAsync(eventName, args);
            });
            Settle(raised, rendersBefore);
        }
        catch (AggregateException error) when (error.InnerExceptions.Count == 1)
        {
            var inner = error.InnerExceptions[0];
            if (GoneHandler(inner))
            {
                throw new StaleElementException(_locator, inner, $"Its {eventName} handler was rendered anew.");
            }

            if (inner is BruinetException)
            {
                throw inner;
            }

            throw BunitTestContext.HandlerFailed(eventName, inner);
        }
        catch (Exception error) when (GoneHandler(error))
        {
            // A render replaced the element between finding it and raising the event: stale, as in a browser; the event never ran,
            // so the call may find it again.
            throw new StaleElementException(_locator, error, $"Its {eventName} handler was rendered anew.");
        }
        catch (Exception error) when (error is not BruinetException)
        {
            throw BunitTestContext.HandlerFailed(eventName, error);
        }

        _context.ThrowIfAppFailed();
    }

    // A user's entry, as a browser's DOM keeps it: Blazor's diff writes the value only where a render changed it, so an
    // entry the app rendered no new value for (a refused try, a picker reset to its placeholder) stays what the user entered.
    private void Entering(string value, Action raise)
    {
        var before = Node.GetAttribute("value");
        raise();
        var node = Node;
        var after = node.GetAttribute("value");
        _context.Enter(KeyOf(node), after, string.Equals(before, after, StringComparison.Ordinal) ? value : null);
    }

    private static string KeyOf(IElement node)
    {
        var parts = new List<int>();
        for (var at = node; at.ParentElement is { } parent; at = parent)
        {
            parts.Add(parent.Children.Index(at));
        }

        parts.Reverse();
        return string.Join('/', parts);
    }

    private static string RenderedValue(IElement node) => node switch
    {
        IHtmlInputElement input => node.GetAttribute("value") ?? input.Value,
        IHtmlTextAreaElement area => node.GetAttribute("value") ?? area.Value,
        IHtmlSelectElement select => select.Value ?? string.Empty,
        _ => node.GetAttribute("value") ?? string.Empty,
    };

    // The element that takes an event: itself or the nearest one around it, as a browser's event bubbles.
    private static IElement? Handling(IElement node, string eventName)
    {
        for (IElement? at = node; at is not null; at = at.ParentElement)
        {
            if (Handles(at, eventName))
            {
                return at;
            }
        }

        return null;
    }

    // How many levels above the element the target is, by their places; null when it is not one of them.
    private static int? LevelsUp(IElement element, IElement target)
    {
        var below = KeyOf(element).Split('/');
        var above = KeyOf(target).Split('/');
        if (above.Length > below.Length || !below.Take(above.Length).SequenceEqual(above))
        {
            return null;
        }

        return below.Length - above.Length;
    }

    private static IElement? Up(IElement? element, int levels)
    {
        for (var i = 0; i < levels && element is not null; i++)
        {
            element = element.ParentElement;
        }

        return element;
    }

    // Waits for the handler as a browser's event returns: until it is done, or until it has rendered and then awaited with the
    // renderer quiet for QuietMs, at most DispatchWaitMs. A handler that shows a dialog and awaits the user's answer goes on
    // just after the dialog renders, where waiting out the whole 100 ms made each such click cost it (next-plan item 12). One
    // that awaits work before it renders anything (a query, a save) is waited for as before: going on early let the next step
    // race it (the studio's PLT.23.6 and PLT.39.2). A fault, its own or a gone handler's, is thrown here as Wait throws it.
    // before: the render count as the event was raised; a render inside the dispatch (the dialog) counts.
    private void Settle(Task raised, int before)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        var renders = before;
        while (!raised.Wait(QuietMs) && waited.ElapsedMilliseconds < DispatchWaitMs)
        {
            var now = _context.RenderCount;
            if (now == renders && now != before)
            {
                return;
            }

            renders = now;
        }
    }

    // How long an event is waited for before the call goes on: a handler that awaits longer runs on, as in a browser.
    private const int DispatchWaitMs = 100;

    // A spell with no render: the handler waits on something that is not the page.
    private const int QuietMs = 5;

    // Blazor's renderer refuses an event whose handler id a newer render dropped.
    internal static bool GoneHandler(Exception error)
    {
        for (Exception? at = error; at is not null; at = at is AggregateException many ? many.InnerExceptions.FirstOrDefault() : at.InnerException)
        {
            if (at is ArgumentException && at.Message.Contains("no event handler", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // A submit button: a <button> is one unless its type says otherwise.
    private static bool SubmitsForm(IElement node)
        => node is IHtmlButtonElement { Type: "submit" } or IHtmlInputElement { Type: "submit" };

    private static bool IsRadio(IElement node) => node is IHtmlInputElement { Type: "radio" };

    // As a browser's :disabled: the element's own attribute, or a form control in a disabled fieldset, outside that fieldset's
    // first legend.
    private static bool Disabled(IElement node)
    {
        if (node.HasAttribute("disabled"))
        {
            return true;
        }

        if (node is not (IHtmlButtonElement or IHtmlInputElement or IHtmlSelectElement or IHtmlTextAreaElement))
        {
            return false;
        }

        for (var (below, at) = (node, node.ParentElement); at is not null; (below, at) = (at, at.ParentElement))
        {
            if (at is IHtmlFieldSetElement && at.HasAttribute("disabled") && below != at.Children.FirstOrDefault(child => child is IHtmlLegendElement))
            {
                return true;
            }
        }

        return false;
    }

    // Inside a closed <details>, all but its first <summary> is folded away.
    private bool Folded(IElement node)
        => node.ParentElement is IHtmlDetailsElement details && !IsOpen(details) && node != SummaryOf(details);

    // Open as rendered, unless the user toggled it since.
    private bool IsOpen(IElement details) => details.HasAttribute("open") ^ _context.Toggled(KeyOf(details));

    private static IElement? SummaryOf(IElement details) => details.Children.FirstOrDefault(child => child.LocalName == "summary");

    // The fold a click toggles: the one whose summary the click is in, or the closed fold itself, whose summary is all it shows.
    private IElement? FoldToggledBy(IElement node)
    {
        if (node is IHtmlDetailsElement closed && !IsOpen(closed))
        {
            return closed;
        }

        for (IElement? at = node; at?.ParentElement is { } parent; at = parent)
        {
            if (parent is IHtmlDetailsElement details && at == SummaryOf(details))
            {
                return details;
            }
        }

        return null;
    }

    // bUnit writes a handled event as a blazor:on… attribute on the element.
    private static bool Handles(IElement element, string eventName) => element.HasAttribute($"blazor:{eventName}");

    // @on…:preventDefault on the element or one around it, which the event passes through.
    private static bool PreventsDefault(IElement node, string eventName)
    {
        for (IElement? at = node; at is not null; at = at.ParentElement)
        {
            if (at.HasAttribute($"blazor:{eventName}:preventDefault"))
            {
                return true;
            }
        }

        return false;
    }

    // Blazor's error banner is hidden by its style sheet and shown only by its script, which nothing runs here; a handler's
    // failure surfaces as AppUnavailableException instead.
    private static bool IsBlazorErrorUi(IElement node) => string.Equals(node.Id, "blazor-error-ui", StringComparison.Ordinal);

    private static bool HidesItself(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return false;
        }

        var compact = style.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return compact.Contains("display:none", StringComparison.Ordinal) || compact.Contains("visibility:hidden", StringComparison.Ordinal);
    }
}
