---
name: html-ui-test
description: Write or fix Bruinet Html UI tests and the page objects, containers and collections they use, going only through page objects and ControlObjects - never the driver, IHtmlElement, Playwright types, or Find* calls. Use when asked to add, write, extend, or fix an Html or Blazor UI test, a web page object, or a test in tests/Bruinet.Html.UITests, tests/Bruinet.Blazor.UITests or samples/Todo/tests/Bruinet.Samples.Todo.Web.UITests, or a control unit test in tests/Bruinet.Html.Tests; also when a web test must run on the bUnit head (BRUINET_HEAD=bunit) or be marked BrowserFact or @browser, or when picking which web tests a change reaches (tools/Scripts/select-tests.ps1, bruinet tests affected).
---

# Write Html UI tests

The same rule as MAUI: a test reads like what a user does and sees. Finding elements, waiting,
readiness and failure messages live in page objects and ControlObjects. When the test needs
something they do not offer, add it there first; never reach past them.

## Workflow

1. **Find the page object** (`PageObjects/` or `Pages/` in the test project). A Blazor page
   derives from `BlazorPageObjectBase<T>`, a plain web page from `PageObjectBase<T>`. If a
   control member is missing, add it with the `html-control` skill first.
2. **Write the test** with page-object members only (shape below).
3. **If the sample lacks what the test needs**, add it to `samples/Bruinet.Samples.Blazor.App`
   with `data-testid`s and a visible effect (a status line). The test project references the
   sample's project: the browser head starts it, the bUnit head renders its `Routes`.
4. **Run the smallest filter that can fail on the bUnit head first**, from the Bruinet root:
   `$env:BRUINET_HEAD = "bunit"; dotnet test tests\Bruinet.Html.UITests --filter "FullyQualifiedName~LoginPageTests"`.
   It runs in about a second, without a browser. To get that filter from the change, run
   `pwsh tools/Scripts/select-tests.ps1` (`bruinet tests affected`): it prints the tests and scenarios the working
   tree's change reaches, each with why, and one `--filter`, and says when shared code (`src/`, the test base, the
   sample's layout) runs the suite whole. `-Run` runs them, `-Head playwright` in the browser. A new page object or test
   is not in the suite's `touch-map.json` until `-WriteMap` after a full run: name it in the filter yourself.
5. **Then once in the browser** (`Remove-Item Env:BRUINET_HEAD`, the same filter) before calling
   it done: the browser judges. One UI test process at a time. Nothing needs to be running: the
   fixture starts the app on a free port (`WebAppHost`). `BLAZOR_APP_URL` points it at a running
   one instead.

## A UI test or a scenario, not both

A behaviour is checked once: by a UAT scenario (`Features/*.feature.md`) or by a code UI test (`Tests/`), never by both.

- A scenario holds what a user does and sees, step by step, in words a reader checks against what was asked for.
- A code UI test holds what a scenario says badly: layout at a size, an old link landing on its page, a lockout, a head's
  own detail.
- Before writing either, look for the same behaviour in the other: the feature files' steps (MCP `list_steps`,
  `find_step_usages`) and the test classes.
- Where both exist, move the code test's extra checks into the scenario as steps and delete the code test. A smoke test
  that goes is replaced in the smoke filter by its scenario, by name (`DisplayName~<id>`).

## Test shape

```csharp
public sealed class FramesPageTests : BlazorSampleTestBase
{
    [BrowserFact("an iframe is its own document, which only a browser loads")]
    public void AControlInTheFrame_IsFoundInTheFrame_AndActsThere()
    {
        NavigateToPage("/frames");
        var page = new FramesPage(Context);

        page.InnerButton.Click();

        page.InnerStatus.AssertText("Clicked in the frame");
    }
}
```

- `[Fact]` for a test that runs on both heads; this one is `[BrowserFact]` because frames need a browser.
- Navigate through the test base (`NavigateToPage`). It waits until the app is interactive.
- Assert with the controls' `Assert*` members, which wait. Read with `Get*`/`Is*` for xUnit
  `Assert` only when a chain cannot say it. No FluentAssertions.
- Assert on what the user sees (text, value, checked state, a row), never on app state.

## Rules that are Html's own

- **Locate by `data-testid`** in page objects: `Locator.ByDataTestId("TodoEdit_Title")`, or a
  CSS string `"[data-testid='x']"`. `Locator.ByAutomationId` means `data-automation-id` or `id`
  in Playwright.
- **Rows:** use a `CollectionObjectBase` whose rows carry `data-item-key`. A row object then
  follows its item through re-renders and `<Virtualize>` node reuse, and never acts on another.
- **Dialogs over a busy page:** a container for a dialog sets `AsksParent => false`, or its
  buttons wait for a page that stays busy until they are pressed. To check that a dialog is
  gone, use a page-level control; the dialog container cannot answer when it is absent.
- **Frames:** declare a `FrameScope<TPage>` and put the frame's controls on it.
- **Page predicates** use `Try*` reads (`TryGetText`), never a `Get*` call inside another call.

## Two heads: browser and bUnit

`Bruinet.Html.UITests` and `Bruinet.Blazor.UITests` run on both heads unchanged ([the guide](../../../docs/guides/bunit.md),
AD-030); the Todo web suites have no bUnit head yet. Write every web test so it can.

- **A test that needs a browser** says so: `[BrowserFact("an iframe is its own document")]`
  instead of `[Fact]`, `[UiFact(Browser = "why")]` for a `UiFact`, `@browser` on a scenario. It is skipped on the bUnit head with the reason.
- **What needs one:** layout and style (positions, sizes, CSS values, hover, swipe), script
  (`Evaluate`, a handler bound in JavaScript), frames (`FrameScope`), recordings and videos,
  XPath and accessible-name locators. On bUnit these end the call at once with
  `BrowserOnlyException` naming the member, which tells you to mark the test.
- **Visible on bUnit** means no `hidden` attribute and no inline `display:none`; a class that hides
  through a style sheet still reads as visible. A test about CSS hiding is a `BrowserFact`.
- **Never change what a test checks to make it pass on one head.** A test that passes in the
  browser and fails on bUnit is a driver gap or a browser-only test: mark it, or fix the driver
  in `src/Bruinet.Html.Bunit`.

## Never in tests, page objects' public members or test helpers

The list in [forbidden-apis.md](references/forbidden-apis.md), mirrored from MAUI's: no
`Context.Driver`, no `IHtmlElement`, no `Find*`/`TryFindElement`, no `Locator` built in a test
method, no Playwright types (`IPage`, `ILocator`, `IFrame`), and no sleeps or hand-written wait
loops.
