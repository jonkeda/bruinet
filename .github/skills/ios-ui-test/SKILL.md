---
name: ios-ui-test
description: Write or fix Bruinet native iOS UI tests and UAT scenarios and the page objects, containers and collections they use, going only through page objects and ControlObjects - never the driver, INativeIosElement, Appium types, simctl, or Find* calls. Use when asked to add, write, extend, or fix a native iOS UI test, page object or feature file in tests/Bruinet.NativeIos.UITests, or a control unit test in tests/Bruinet.NativeIos.Tests.
---

# Write native iOS UI tests

The same rule as MAUI, Html and native Android: a test reads like what a user does and sees.
Finding views, waiting, readiness and failure messages live in page objects and ControlObjects.
When the test needs something they do not offer, add it there first with the `ios-control` skill.

## Workflow

1. **Find the page object** (`Pages/` in the test project). A page derives from
   `PageObjectBase<TSelf>`, names a `ReadyLocator`, and carries `[TestPage("Sign In")]` so UAT
   scenarios find it under that name. iOS has no activity.
2. **Write the test** with page-object members only (shape below), in the `Gallery` collection:
   one Appium session for the whole suite.
3. **If the sample lacks what the test needs**, add it to `samples/Bruinet.Samples.NativeIos.App`
   with an accessibility identifier and a visible effect (a status line), and rebuild it: the
   fixture installs the `.app` just built. Where the Android gallery has the screen, copy its
   identifiers and texts, so the shared feature files run on both.
4. **Run the smallest filter that can fail**, from the Bruinet root on a Mac, with a simulator and
   Appium up (`docs/run/native-ios.md`):
   `dotnet test tests/Bruinet.NativeIos.UITests --filter "FullyQualifiedName~ControlsTests"`.
   One UI test process at a time; no build while one runs.

## A UI test or a scenario, not both

A behaviour is checked once, by a UAT scenario or by a code UI test: the rule and what goes where are in
[AGENTS.md, "A UAT scenario or a code UI test"](../../../AGENTS.md#a-uat-scenario-or-a-code-ui-test).

## Test shape

```csharp
[Collection(GalleryCollection.Name)]
public sealed class LoginTests(GalleryFixture gallery)
{
    [Fact]
    public void SigningIn_WaitsOutTheBusyMoment_AndGreetsTheUser()
    {
        gallery.Open("login");
        var page = new LoginPage(gallery.Context);
        page.AssertIdle();

        page.UserName.SetText("daan");
        page.Password.SetText("s3cret");
        page.SignIn.Click();

        page.Status.AssertText("Welcome, daan");
    }
}
```

- Open a screen by its deep link (`gallery.Open("login")`), then `AssertIdle` on its page.
- Assert with the controls' `Assert*` members, which wait. No FluentAssertions.
- **The app's busy view waits for you.** A view with accessibility identifier `bruinet_busy` that
  is shown makes the page busy; the next call waits it out. Never wait for a spinner by hand.

## Rules that are iOS's own

- **Locate by accessibility identifier** (`"username"`), then label (`NativeIosLocator.ByLabel`),
  then text. XPath is the last resort: it makes XCUITest serialize the whole tree.
- **Rows:** use a collection whose rows are keyed by a view's text
  (`new TableView<Page>(this, "invoices", itemKeyId: "name")`) and get rows with
  `Item("Invoice 42")`: it scrolls the row onto the screen. A row far down a long table takes
  several seconds; that is the cost of XCUITest, not a hang.
- **Dialogs:** `Alert<Page>` and `ActionSheet<Page>` are scopes shown over the page.
  `PermissionAlert<Page>` is Springboard's and covers the app. The fixture turns
  `AutoAcceptAlerts` off, so a test that does not test the prompt answers it.
- **"Save Password?"** can cover the screen after a password field; the page then reports its
  ready element as not shown and names the sheet.
- **Hover** has no route on iOS and throws; a test of a hover belongs on another head.

## UAT

Feature files are in `Features/` of the native Android UI tests and are shared: their `Target` is
`NATIVEANDROID, NATIVEIOS` and their tag `@native` (AD-014). `Uat/NativeIosUatScenarioTests` runs
them on the same session (`--filter "Category=UAT"`). The control verbs are the built-in ones; a
page's navigation step is a `NavigateTo<Page>()` method on `GalleryFixture`.

## Never in tests, page objects' public members or test helpers

The list in [forbidden-apis.md](references/forbidden-apis.md): no `Context.Driver`, no
`INativeIosElement`, no `Find*`/`TryFindElement`, no `Locator` built in a test method, no Appium or
Selenium types, no `simctl` from a test, no sleeps or hand-written wait loops.
