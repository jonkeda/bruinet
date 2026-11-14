# Playwright Platform Guide

Use Playwright when web tests need reliable auto-waiting, browser tracing, and
modern browser automation.

## Projects

- `src/Bruinet.Html`
- `src/Bruinet.Html.Playwright`
- `src/Bruinet.Blazor`
- `tests/Bruinet.Blazor.Tests`
- `tests/Bruinet.Html.UITests` (the UI tests and the UAT suite)
- `tests/Bruinet.Blazor.UITests` (the UI tests and the UAT suite)
- `samples/Todo/tests/Bruinet.Samples.Todo.Web.UITests`, `samples/Todo/tests/Bruinet.Samples.Todo.Web.Uat`

## Rules

- Keep browser and page lifecycle in fixtures.
- Keep selectors inside page objects and controls.
- Wait through Bruinet members (`WaitX`, `AssertX`), never with fixed delays. A member is one
  synchronous call with one budget, as on MAUI; there is no async API.
- Html controls have MAUI's members and names (`AssertTextContains`, `SetText`, `Enter`,
  `SelectByText`, `AssertSelectedText`), so a test or a UAT scenario reads the same on both.
- Save traces under the shared artifact layout when enabled.

## A site with a sign-in

`HtmlTestContextOptions` holds what a suite needs from the browser context, so a suite writes no Playwright set-up
of its own:

| Option | What it does |
| --- | --- |
| `StorageStatePath` | Starts the context from a saved sign-in; ignored when the file does not exist yet |
| `Locale`, `ViewportWidth`, `ViewportHeight` | The browser's language and the page's size |
| `AllowedHosts` | The hosts the page may navigate to; a navigation elsewhere is refused and listed in `RefusedNavigations`. Keeps a suite off production |

Sign in once per account, then save the context's state with `SaveStorageState(path)` (git-ignored) for the next run.
The account comes from `TestAccounts` and the password is typed with `SetSecret`
([Test accounts](../guides/test-accounts.md)).

`PlaywrightTestContext.CreateAsync` never resumes on Playwright's own thread, so a synchronous fixture may wait on
it (`.GetAwaiter().GetResult()`). A fixture's own `await` on a Playwright call may resume there, and a Bruinet call
after it then blocks the thread Playwright needs: the test hangs. Keep Playwright calls in Bruinet, or behind
`ConfigureAwait(false)`.

## Tabs

A tab's content is a page object of its own (AD-039): derive it from `TabPageBase<TParent, TSelf>`, with the tab's
locator and the panel's, and declare one per tab on the page. `Open()` selects the tab and returns once it is
selected and its panel shown:

```csharp
public sealed class ExpiredTab(InvoicesPage page)
    : TabPageBase<InvoicesPage, ExpiredTab>(page, Locator.ByCss("[role='tab'][data-tab='expired']"), Locator.ByCss("[role='tabpanel']"));

page.Expired.Open();
```

A UI kit's tab page sets its own locators once (Vuetify's `.v-tab` and active `.v-window-item`), in a library of its own.

## Run

See [Playwright Run Guide](../run/Playwright.md).
