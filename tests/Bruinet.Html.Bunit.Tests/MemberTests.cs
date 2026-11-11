using System.Diagnostics;
using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Html.Bunit.Tests.Components;

namespace Bruinet.Html.Bunit.Tests;

/// <summary>Every <see cref="IHtmlElement"/> member on the bUnit head (AD-030).</summary>
public sealed class MemberTests : IDisposable
{
    private readonly BunitTestContext _context = new(new BunitTestContextOptions { RootComponent = typeof(Members), BaseUrl = "http://localhost/" });

    public void Dispose() => _context.Dispose();

    private IHtmlElement Id(string testId) => _context.FindElement(Locator.ByDataTestId(testId));

    private string Echo(string testId) => Id($"{testId}-echo").Text ?? string.Empty;

    #region Find

    [Fact]
    public void Find_ByDataTestId_FindsTheElement() => Assert.Equal("input", Id("name").TagName);

    [Fact]
    public void Find_ById_FindsTheElement() => Assert.Equal("Apple pie", _context.FindElement(Locator.ById("one")).Text);

    [Fact]
    public void Find_ByName_FindsTheElement() => Assert.Equal("Apple pie", _context.FindElement(Locator.ByName("one")).Text);

    [Fact]
    public void Find_ByClassName_FindsEveryElementWithTheClass() => Assert.Equal(2, _context.FindElements(Locator.ByClassName("item")).Count);

    [Fact]
    public void Find_ByCss_FindsTheElement() => Assert.Equal("Apple pie", _context.FindElement(Locator.ByCss("li.first")).Text);

    [Fact]
    public void Find_ByTagName_FindsEveryElement() => Assert.Equal(2, _context.FindElements(Locator.ByTagName("li")).Count);

    [Fact]
    public void Find_ByAutomationId_FindsByAutomationIdOrId()
    {
        Assert.Equal("Apple pie", _context.FindElement(Locator.ByAutomationId("auto-one")).Text);
        Assert.Equal("Apple pie", _context.FindElement(Locator.ByAutomationId("one")).Text);
    }

    [Fact]
    public void Find_ByText_FindsTheInnermostElement_AnyCase()
    {
        var found = _context.FindElement(Locator.ByText("banana SPLIT"));

        Assert.Equal("li", found.TagName);
    }

    [Fact]
    public void Find_ByLinkText_FindsTheLink() => Assert.Equal("a", _context.FindElement(Locator.ByLinkText("Elsewhere")).TagName);

    [Fact]
    public void Find_ScopedToAParent_FindsOnlyWithinIt()
    {
        var items = _context.FindElements(Locator.ByTagName("b").ScopedTo(Locator.ByDataTestId("list")));

        Assert.Equal("split", Assert.Single(items).Text);
    }

    [Fact]
    public void Find_ChildOfAnElement_FindsWithinIt()
    {
        var list = Id("list");

        Assert.Equal(2, list.FindElements(Locator.ByTagName("li")).Count);
        Assert.Null(list.TryFindElement(Locator.ByDataTestId("name")));
    }

    [Fact]
    public void Find_Missing_IsNullOrNotFound()
    {
        Assert.Null(_context.TryFindElement(Locator.ByDataTestId("nothing")));
        Assert.Throws<ElementNotFoundException>(() => _context.FindElement(Locator.ByDataTestId("nothing")));
    }

    [Fact]
    public void Find_ByXPath_IsNotSupported()
        => Assert.Throws<LocatorNotSupportedException>(() => _context.FindElements(Locator.ByXPath("//li")));

    #endregion

    #region Click

    [Fact]
    public void Click_DisabledButton_RunsNoHandler()
    {
        Id("off").Click();

        Assert.Equal("0", Echo("off"));
    }

    [Fact]
    public void Click_SubmitButtonWithoutAHandler_SubmitsItsForm()
    {
        Id("form-submit").Click();

        Assert.Equal("1", Echo("submit"));
    }

    [Fact]
    public void Click_ButtonWithItsOwnHandlerInAForm_AlsoSubmits()
    {
        Id("form-handled").Click();

        Assert.Equal("1", Echo("handled"));
        Assert.Equal("1", Echo("submit"));
    }

    [Fact]
    public void Click_TypeButtonInAForm_DoesNotSubmit()
    {
        Id("form-plain").Click();

        Assert.Equal("1", Echo("handled"));
        Assert.Equal("0", Echo("submit"));
    }

    [Fact]
    public void Click_AnElementInsideAHandler_BubblesToIt()
    {
        Id("wrap-inner").Click();

        Assert.Equal("1", Echo("wrap"));
    }

    [Fact]
    public void Click_Link_FollowsItsHref()
    {
        Id("link").Click();

        // Only the address: this component has no router to render the new page.
        Assert.Equal("http://localhost/elsewhere", _context.CurrentUrl);
    }

    // As a browser: the handler runs, then the link is followed.
    [Fact]
    public void Click_LinkWithItsOwnHandler_RunsItAndFollowsTheHref()
    {
        Id("link-handled").Click();

        Assert.Equal("1", Echo("link"));
        Assert.Equal("http://localhost/handled", _context.CurrentUrl);
    }

    [Fact]
    public void Click_LinkThatPreventsDefault_RunsItsHandlerOnly()
    {
        Id("link-prevented").Click();

        Assert.Equal("1", Echo("link"));
        Assert.Equal("http://localhost/", _context.CurrentUrl);
    }

    [Fact]
    public void Click_DoubleAndRight_RaiseTheirEvents()
    {
        Id("pointer").DoubleClick();
        Assert.Equal("double", Echo("pointer"));

        Id("pointer").RightClick();
        Assert.Equal("right", Echo("pointer"));
    }

    // As in a browser: these bubble to the nearest element that listens (review 04, finding 6).
    [Fact]
    public void Click_DoubleAndRight_OnAChild_ReachTheParentsHandlers()
    {
        Id("pointer-inner").DoubleClick();
        Assert.Equal("double", Echo("pointer"));

        Id("pointer-inner").RightClick();
        Assert.Equal("right", Echo("pointer"));
    }

    [Fact]
    public void Key_OnAChild_ReachesTheParentsHandler()
    {
        Id("key-inner").PressKey("Escape");

        Assert.Equal("Escape", Echo("keys"));
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Key_EnterOrSpace_OnAButton_IsItsClick(string key)
    {
        Id("wrap").PressKey(key);

        Assert.Equal("1", Echo("wrap"));
    }

    [Fact]
    public void Click_OnALabel_SaysItNeedsTheBrowser_RatherThanDoingNothing()
        => Assert.Throws<BrowserOnlyException>(() => Id("label").Click());

    // As a browser reads them (review 04, findings 4 and 5).
    [Fact]
    public void State_ReadsAriaAndLeavesHiddenTextOut()
    {
        Assert.False(Id("aria-off").Enabled);
        Assert.True(Id("div-disabled").Enabled);
        Assert.True(Id("aria-switch").IsChecked);
        Assert.Equal("Shown", Id("hidden-children").Text);
    }

    [Fact]
    public void Click_HandlerThrows_IsTheAppFailing()
    {
        var error = Assert.Throws<AppUnavailableException>(() => Id("throws").Click());

        Assert.Contains("the handler broke", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Click_HandlerThrowsAfterItsAwait_TheNextLookReportsIt()
    {
        Id("throws-later").Click();
        Thread.Sleep(600);

        var error = Assert.Throws<AppUnavailableException>(() => Id("name"));
        Assert.Contains("the handler broke later", error.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Fill

    [Fact]
    public void Fill_BoundOnChange_SetsTheValue()
    {
        Id("name").Fill("Ada");

        Assert.Equal("Ada", Echo("name"));
        Assert.Equal("Ada", Id("name").InputValue);
    }

    [Fact]
    public void Fill_BoundOnInput_SetsTheValue()
    {
        Id("live").Fill("typing");

        Assert.Equal("typing", Echo("live"));
    }

    // As a browser's DOM: the render wrote no new value, so the field keeps what was typed.
    [Fact]
    public void Fill_AValueTheAppDoesNotRenderAgain_StaysWhatWasEntered()
    {
        Id("refuse").Fill("5");

        Assert.Equal("5", Id("refuse").InputValue);
    }

    [Fact]
    public void Fill_AValueTheAppRendersAnew_ReadsTheRenderedValue()
    {
        Id("number").Fill("07");

        Assert.Equal("7", Id("number").InputValue);
    }

    [Fact]
    public void Select_APickerThatResets_ShowsThePickUntilARenderChangesIt()
    {
        Id("adder").SelectOption("dog");

        Assert.Equal("dog", Echo("added"));
        Assert.Equal("dog", Id("adder").InputValue);
    }

    [Fact]
    public void Fill_TheNextAction_ForgetsTheEntry()
    {
        Id("refuse").Fill("5");

        Id("wrap").Click();

        Assert.Equal("20", Id("refuse").InputValue);
    }

    [Fact]
    public void Fill_AfterAReset_IsForgotten()
    {
        Id("refuse").Fill("5");

        _context.ResetAppState();

        Assert.Equal("20", Id("refuse").InputValue);
    }

    [Fact]
    public void Fill_TextArea_SetsTheValue()
    {
        Id("notes").Fill("a note");

        Assert.Equal("a note", Echo("notes"));
    }

    [Fact]
    public void Fill_WithoutAHandler_HoldsTheValue()
    {
        Id("plain").Fill("second");

        Assert.Equal("second", Id("plain").InputValue);
    }

    [Fact]
    public void Fill_SendKeysAndClear_AppendAndEmpty()
    {
        Id("name").Fill("Ada");
        Id("name").SendKeys(" L", TextInputMethod.SetValue);
        Assert.Equal("Ada L", Echo("name"));

        Id("name").Clear();
        Assert.Equal(string.Empty, Echo("name"));
    }

    #endregion

    #region Check and select

    [Fact]
    public void Check_BoundCheckbox_SetsAndClears()
    {
        Id("agree").Check();
        Assert.Equal("True", Echo("agree"));
        Assert.True(Id("agree").IsChecked);

        Id("agree").Uncheck();
        Assert.Equal("False", Echo("agree"));
    }

    [Fact]
    public void Check_ClickHandledCheckbox_Clicks()
    {
        Id("tick").Check();

        Assert.Equal("True", Echo("tick"));
    }

    // A browser's change event on a radio carries its value, not true.
    [Fact]
    public void Check_Radio_SendsItsValue()
    {
        Id("size-s").Check();

        Assert.Equal("small", Echo("size"));
    }

    [Fact]
    public void Check_RadioWithoutAValue_SendsOn()
    {
        Id("size-plain").Check();

        Assert.Equal("on", Echo("size"));
    }

    [Fact]
    public void Check_InputRadio_BindsItsValue()
    {
        Id("pet-dog").Check();

        Assert.Equal("dog", Echo("pet"));
        Assert.True(Id("pet-dog").IsChecked);
        Assert.False(Id("pet-cat").IsChecked);
    }

    [Fact]
    public void Select_Option_SetsTheValue()
    {
        Id("colour").SelectOption("green");

        Assert.Equal("green", Echo("colour"));
        Assert.Equal("green", Id("colour").InputValue);
    }

    [Fact]
    public void Select_Options_SetsEveryValue()
    {
        Id("sizes").SelectOption(["s", "l"]);

        Assert.Equal("s,l", Echo("sizes"));
    }

    #endregion

    #region Keys and focus

    [Fact]
    public void Key_PressKey_RaisesKeyDown()
    {
        Id("keys").PressKey("Escape");

        Assert.Equal("Escape", Echo("keys"));
    }

    [Fact]
    public void Key_SendKeys_RaisesAKeyPerCharacter()
    {
        Id("keys").SendKeys("ab");

        Assert.Equal("ab", Echo("keys"));
    }

    [Fact]
    public void Key_EnterInAFormField_SubmitsTheForm()
    {
        Id("form-field").PressKey("Enter");

        Assert.Equal("1", Echo("submit"));
    }

    [Fact]
    public void Key_FocusAndBlur_RaiseTheirEvents()
    {
        Id("keys").Focus();
        Assert.Equal("in", Echo("focus"));

        Id("keys").Blur();
        Assert.Equal("out", Echo("focus"));
    }

    [Fact]
    public void Key_SameElementAfterRender_SameKey()
    {
        var before = Id("name-echo").InstanceKey;
        Id("name").Fill("Ada");

        Assert.Equal(before, Id("name-echo").InstanceKey);
        Assert.NotEqual(before, Id("live-echo").InstanceKey);
    }

    #endregion

    #region State and reads

    [Fact]
    public void Visible_HiddenOrStyledAway_IsNotVisible()
    {
        Assert.True(Id("name").Visible);
        Assert.False(Id("hidden").Visible);
        Assert.False(Id("hidden-child").Visible);
        Assert.False(Id("invisible").Visible);
        Assert.False(Id("error-ui").Visible);
    }

    [Fact]
    public void State_EnabledAndSelected_FromTheDom()
    {
        Assert.False(Id("off").Enabled);
        Assert.True(Id("name").Enabled);
        Assert.True(_context.FindElement(Locator.ByCss("[data-testid='colour'] option[value='red']")).Selected);
    }

    [Fact]
    public void Read_TextAttributesAndHtml_FromTheDom()
    {
        var item = _context.FindElement(Locator.ById("one"));

        Assert.Equal("Apple pie", item.Text);
        Assert.Equal("item first", item.GetAttribute("class"));
        Assert.Equal("item first", item.GetDomAttribute("class"));
        Assert.Equal("li", item.TagName);
        Assert.Equal("Apple pie", item.InnerHtml);
        Assert.StartsWith("<li", item.OuterHtml, StringComparison.Ordinal);
        Assert.Equal("Apple pie", item.GetDomProperty("textContent"));
        Assert.Equal("true", Id("off").GetDomProperty("disabled"));
    }

    // As a Playwright locator: an element found before a reset is found again in the new render.
    [Fact]
    public void Read_AfterAReset_FindsTheElementAgain()
    {
        var item = _context.FindElements(Locator.ByTagName("li"))[1];
        _context.NavigateTo("http://localhost/elsewhere");
        _context.ResetAppState();

        Assert.Equal("Banana split", item.Text);
    }

    // What the browser shows without a style sheet: a hidden input, a closed fold, a closed dialog.
    [Theory]
    [InlineData("hidden-input", false)]
    [InlineData("closed-summary", true)]
    [InlineData("folded", false)]
    [InlineData("unfolded", true)]
    [InlineData("closed-dialog", false)]
    [InlineData("in-dialog", false)]
    [InlineData("in-open-dialog", true)]
    public void Visible_MarkupThatHides_IsNotVisible(string testId, bool visible) => Assert.Equal(visible, Id(testId).Visible);

    // As a browser's fold: a click on its summary, or on the closed fold itself, opens it; again on the summary closes it.
    [Fact]
    public void Click_TheSummaryOfAClosedFold_OpensAndClosesIt()
    {
        Id("closed-summary").Click();
        Assert.True(Id("folded").Visible);

        Id("closed-summary").Click();
        Assert.False(Id("folded").Visible);
    }

    [Fact]
    public void Click_AClosedFold_OpensIt()
    {
        Id("closed-details").Click();

        Assert.True(Id("folded").Visible);
    }

    [Fact]
    public void Click_AnOpenedFold_StaysOpenUntilAPageLoad()
    {
        Id("closed-summary").Click();
        Id("wrap").Click();
        Assert.True(Id("folded").Visible);

        _context.Refresh();

        Assert.False(Id("folded").Visible);
    }

    [Fact]
    public void State_InADisabledFieldset_IsDisabledAndGetsNoClick()
    {
        Assert.False(Id("fenced").Enabled);

        Id("fenced").Click();

        Assert.Equal("0", Echo("fenced"));
    }

    [Fact]
    public void State_InTheLegendOfADisabledFieldset_IsEnabled() => Assert.True(Id("in-legend").Enabled);

    #endregion

    #region Bubbling

    [Fact]
    public void Click_InsideAnElementThatHandlesIt_RaisesBoth()
    {
        Id("row-button").Click();

        Assert.Equal("1", Echo("row-button"));
        Assert.Equal("1", Echo("row"));
    }

    [Fact]
    public void Click_ThatStopsPropagation_RaisesItsOwnOnly()
    {
        Id("row-stop").Click();

        Assert.Equal("1", Echo("row-button"));
        Assert.Equal("0", Echo("row"));
    }

    #endregion

    #region Browser-only

    public static TheoryData<string, Action<IHtmlElement>> BrowserOnlyMembers => new()
    {
        { "Location", e => _ = e.Location },
        { "Size", e => _ = e.Size },
        { "Rect", e => _ = e.Rect },
        { "GetCssValue", e => e.GetCssValue("color") },
        { "ClickAt", e => e.ClickAt(1, 1) },
        { "Hover", e => e.Hover() },
        { "LongPress", e => e.LongPress() },
        { "Swipe", e => e.Swipe(0, 0, 1, 1) },
        { "Evaluate", e => e.Evaluate("1") },
        { "Evaluate", e => e.Evaluate<int>("1") },
        { "TryGetFrameDocument", e => e.TryGetFrameDocument() },
    };

    [Theory]
    [MemberData(nameof(BrowserOnlyMembers))]
    public void BrowserOnly_Member_Throws(string member, Action<IHtmlElement> ask)
    {
        var error = Assert.Throws<BrowserOnlyException>(() => ask(Id("name")));

        Assert.Equal(member, error.Member);
    }

    [Fact]
    public void BrowserOnly_InAControlCall_EndsTheCallAtOnce()
    {
        using var probe = new BunitTestContext(new BunitTestContextOptions { RootComponent = typeof(Probe) });
        var page = new ProbePage(probe);

        var clock = Stopwatch.StartNew();
        var error = Record.Exception(() => page.Slow.Hover(timeoutMs: 3000));

        Assert.NotNull(error);
        Assert.True(Contains<BrowserOnlyException>(error), $"Expected a BrowserOnlyException, got {error}");
        Assert.True(clock.ElapsedMilliseconds < 1000, $"The call took {clock.ElapsedMilliseconds} ms; a browser-only member must end it at once.");
    }

    [Fact]
    public void A_ControlNotFound_SaysTheCall_ThePage_AndTheTimeWaited()
    {
        using var probe = new BunitTestContext(new BunitTestContextOptions { RootComponent = typeof(Probe) });
        var page = new ProbePage(probe);

        var error = Assert.Throws<ElementNotFoundException>(() => page.Missing.Click(timeoutMs: 200));

        Assert.StartsWith("Click on control 'Missing' (Css:[data-testid='no-such-button']) of page 'ProbePage': not found within 200 ms.", error.Message, StringComparison.Ordinal);
        Assert.Contains(" Shown on the page: ", error.Message, StringComparison.Ordinal);
        Assert.Contains("Increment", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing,", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserOnly_ScrollIntoView_IsNothingToDo() => Id("name").ScrollIntoView(100);

    #endregion

    private static bool Contains<T>(Exception? error)
        where T : Exception
    {
        for (; error is not null; error = error.InnerException)
        {
            if (error is T)
            {
                return true;
            }
        }

        return false;
    }
}
