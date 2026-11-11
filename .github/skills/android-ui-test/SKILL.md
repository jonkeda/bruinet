---
name: android-ui-test
description: Write or fix Bruinet native Android UI tests and UAT scenarios and the page objects, containers and collections they use, going only through page objects and ControlObjects - never the driver, INativeAndroidElement, Appium types, adb, or Find* calls. Use when asked to add, write, extend, or fix a native Android UI test, page object or feature file in tests/Bruinet.NativeAndroid.UITests, or a control unit test in tests/Bruinet.NativeAndroid.Tests.
---

# Write native Android UI tests

The same rule as MAUI and Html: a test reads like what a user does and sees. Finding views,
waiting, readiness and failure messages live in page objects and ControlObjects. When the test
needs something they do not offer, add it there first with the `android-control` skill.

## Workflow

1. **Find the page object** (`Pages/` in the test project). A page derives from
   `PageObjectBase<TSelf>`, names its `Activity` (`".LoginActivity"`) and a `ReadyLocator`, and
   carries `[TestPage("Sign In")]` so UAT scenarios find it under that name.
2. **Write the test** with page-object members only (shape below), in the `Gallery` collection:
   one Appium session for the whole suite.
3. **If the sample lacks what the test needs**, add it to `samples/Bruinet.Samples.NativeAndroid.App`
   with a resource id and a visible effect (a status line), and rebuild it: the fixture installs
   the APK just built.
4. **Run the smallest filter that can fail**, from the Bruinet root, with the emulator and Appium
   up (`docs/run/native-android.md`):
   `dotnet test tests\Bruinet.NativeAndroid.UITests --filter "FullyQualifiedName~ControlsTests"`.
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
- **The app's busy view waits for you.** A view with resource id `bruinet_busy` that is shown
  makes the page busy; the next call waits it out. Never wait for a spinner by hand.
- **Something that leaves by itself** (a snackbar) is asserted first after the action that shows it.

## Rules that are Android's own

- **Locate by resource id** (`"username"`, the package prefix is added), then content description
  (`Locator.ByAccessibilityId`), then text. XPath is the last resort: every XPath makes UiAutomator2
  serialize the whole screen.
- **Rows:** a `RecyclerView` recycles its views. Use a collection whose rows are keyed by a view's
  text (`new RecyclerView<Page>(this, "invoices", itemKeyId: "name")`) and get rows with
  `Item("Invoice 42")`: it scrolls the list to the item and the row follows its item.
- **Dialogs:** `AlertDialog<Page>` and `PermissionDialog<Page>` are scopes shown over the page; the
  permission prompt is the system's and covers the app, which is then "not ready" until it is answered.
- **Hover** has no route on Android and throws; a test of a hover belongs on another head.

## UAT

Feature files are in `Features/`, run by `Uat/NativeAndroidUatScenarioTests` on the same session
(`--filter "Category=UAT"`). The control verbs are the built-in ones MAUI and Html use; a page's
navigation step is a `NavigateTo<Page>()` method on `GalleryFixture`.

## Never in tests, page objects' public members or test helpers

The list in [forbidden-apis.md](references/forbidden-apis.md): no `Context.Driver`, no
`INativeAndroidElement`, no `Find*`/`TryFindElement`, no `Locator` built in a test method, no
Appium or Selenium types, no `adb` from a test, no sleeps or hand-written wait loops.
