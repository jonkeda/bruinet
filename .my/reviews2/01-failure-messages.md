# Bruinet's failure messages: review and proposals

Written 2026-11-11 at `b2ea4570`. Review 1 of `additional-reviews-proposal.md`.
Failures were provoked in a throwaway worktree, which is removed; nothing in the checkout was changed.

## Outcome

A red test names the locator and, for an assertion, what was expected and seen. It does not name the page object member, and a control that is not found names neither its page nor how long was waited.
The scope messages are the model to copy: they name the scope, the call, the control, the budget and the reason in one sentence.
The BDD bind errors and the two replay misses are the best messages in the product. The BDD run-time failure is the worst to read, because one failed step arrives under the whole phrase catalog.
No message says what to try next, although the trace already holds the answer.

## What was run and what was read

| Stack | How | Failures provoked |
| --- | --- | --- |
| Html on Playwright (headless) | Run | 17 in code, 5 scenarios |
| Html on bUnit | Run | The same 17 and 5 |
| Blazor, both heads | Run | 5 on each head |
| MAUI Headless | Run | 7 |
| SQL and HTTP replay | Run | 2 misses each |
| WPF, WinForms, MAUI FlaUI | Read from source | None |
| MAUI Appium, native Android, Stride | Read from source | None |

`failing`, `last_runs`, `get_trace` and `validate_scenario` were called over `bruinet mcp` on the Playwright run's results.
Every quoted message below is what the run printed. A file and line marks what was read and not run.

## The messages, failure by failure

These are from Html. Both heads gave the same text unless the table says otherwise.

| Failure | Message | Judgement |
| --- | --- | --- |
| Wrong locator | `Element not found with locator: Css='[data-testid='incremnt-btn']'` | Locator only |
| Wrong text | `Expected Text to be 'Current count: 99'. Locator: Css:[data-testid='count-display']` then `Actual: 'Current count: 0'` | Good |
| Disabled control | `The element 'Css:#disabled-checkbox' was found but is disabled: not enabled still after 2000 ms` | Clear, clumsy |
| Page never interactive | `Scope 'SampleShell' did not become ready for Click on control 'Css:[…]' within 2000 ms. Last readiness state: NotLoaded (no data-busy on <body> yet: the app is not interactive).` | Best of all |
| Wrong page | `Expected page 'ProbeLoginPage' to be loaded but it is not.` | No url, no time |
| Server not there | `net::ERR_CONNECTION_REFUSED at http://127.0.0.1:59321/counter` | Raw Playwright |
| Row that is gone | `Scope 'TodoRow(item 'item-001')' did not become ready for Click on control 'Css:[…]' within 500 ms. Last readiness state: ItemChanged (item 'item-001' is no longer shown in the list).` | Good |
| Missing phrase | `probe.feature.md:17: UATB001 No command binding matches step 'I frobnicate Increment'.` | Good, no nearest |
| Unknown control | `UATB009 Step 'I tap Incremnt' does not fit the Counter page: The Counter page has no control 'Incremnt'. Its controls are Broken, Count, Increment, Page.` | Good |
| Missing SQL recording | `No recording at …\none.json: record the test first, twice.` | Good, no how |
| HTTP call not recorded | `HTTP replay miss in A test.json: POST /todos; nearest recorded is exchange 0 POST /todos: $.title is '"Bread"' where '"Milk"' was recorded.` | Good |

A stale element could not be provoked as its own failure: every call finds its element again, so a removed element surfaces as "not found" or as the row message above.

## Findings

### 1. The page object member is in no message

- No exception text names `IncrementButton` or `CounterPage`. The control knows only its locator (`src/Bruinet.Html/Controls/ViewBase.tpl.cs:79`, where the log's control id is `Locator.Value`).
- In a BDD run the wrapper adds the phrase's name: `Failed to run 'Click' on control 'Broken': …` (`src/Bruinet.Uat/UatReflectionRuntime.cs:562`). That is the control's name in the scenario, not the member `BrokenButton`.
- The failure JSON's `frames` should point at the suite's code. For both failed scenarios it held one entry, `UatReflectionRuntime.InvokeControlMethodAsync (UatReflectionRuntime.cs:551)`, a Bruinet frame.
- The cause is `IsFramework` in `src/Bruinet.Uat/UatStepFailure.cs:181`, which treats an assembly ending in `.Uat` as the suite's own.

### 2. "Not found" is the thinnest message and the most common failure

- It gives the locator and nothing else: no page, no call, no time waited. The constructor that takes the timeout exists (`ElementNotFoundException.cs:28`) and no stack's `NotFound()` uses it (`ViewBase.tpl.cs:57` in Html, the same line 48 in WPF, WinForms, Stride and native Android).
- MAUI is the one stack that names the page: `Element not found within ProbePage. Child locator: AutomationId:NoSuchButton` (run on MAUI Headless).
- A route the app does not have gives the same text as a typo in a locator: after `/no-such-page` the message was `Element not found with locator: Css='[data-testid='increment-btn']'`.
- The locator is written two ways. "Not found" prints `Css='…'`, every other message prints `Css:…`.
- The trace already knows more: `get_trace` showed the page's controls after the failed step, with `"name":"Broken","shown":false`. The message does not mention the trace.

### 3. A BDD run-time failure is buried under the catalog

- The failed step is one line of the message. Under it come the two discovered pages and all 32 phrases of the catalog (`BddDiagnosticsFormatter.FormatResults`, `src/Bruinet.Bdd/BddDiagnosticsFormatter.cs:95`). On a real suite the catalog is far longer.
- The catalog helps a bind error, where a phrase is missing. It helps nothing when a control was not found or a text was wrong.
- The failed step's line runs together: `Failed: …probe.feature.md:50: Builtin.Control.Tap: I tap Broken Failed to run 'Click' on control 'Broken': Element not found…`.
- The path is the copy in `bin\Debug\net10.0\Features`, not the feature file the author edits.
- Every one of these is thrown as `InvalidOperationException` (`UatScenarioTestBase.cs:226` and `:499`), so the type says nothing.
- `uat.config.md` sets `ScreenshotOnFailure` to true, and both results had `"evidencePath": null` with no word on why.

### 4. A scenario that fails to bind never reaches the tools

- Three of the five broken scenarios failed at binding. `failing` answered `"count":2` and `last_runs` listed two scenarios, because a bind failure writes no result.
- `get_trace` on the missing-phrase scenario answered `No run of it has a result yet: run it with run_scenario.`, although it had just run and failed.
- `validate_scenario` did report all four bind errors with their lines and codes. An agent that starts at `failing`, as the server's instructions say, does not see them.
- A missing phrase gets no nearest match. The message lists the whole catalog and leaves the reader to find `I tap {control}`.

### 5. A page that never loads has no message of its own

- `PageLoadException` is declared and never thrown anywhere in `src` (`git grep "new PageLoadException"` finds nothing). `UatStepFailure.cs:107` still classifies on it.
- On Playwright an unreachable server gives the raw `PlaywrightException` with its call log. `PlaywrightErrors.Live` maps only "closed" and "detached" (`src/Bruinet.Html.Playwright/PlaywrightErrors.cs:43`).
- On bUnit the same `NavigateTo("http://127.0.0.1:59321/counter")` raised nothing at all: the head ignores the host. A wrong base address passes there and fails in the browser.
- On bUnit `NavigateTo("about:blank")` gave `System.ArgumentException: The URI 'http://localhost/' is not contained by the base URI 'about:///'.`, where Playwright gave the readable scope message.
- `AssertLoaded` on the wrong page says only that the page "is not" loaded. Html and MAUI give `NotLoaded` or `MissingRoot` with no detail (`src/Bruinet.Html/Pages/PageObjectBase.cs:116`, `src/Bruinet.Core/Scopes/ContainerObjectBase.cs:77`).
- WPF, WinForms and Stride do better, by reading: their detail is `'{ReadyLocator}' is not shown` (`src/Bruinet.Wpf/Pages/PageObjectBase.cs:84`).

### 6. Smaller things in the wording

- "was found but is disabled: not enabled still after 2000 ms" says disabled twice. MAUI Headless gave "was found but is disabled: still after 2000 ms". The detail `"not enabled"` is the same literal in five stacks (`ViewBase.tpl.cs:321` in Html).
- `AssertTitle` repeats the actual value: `Expected page title 'Login' but got 'Counter - Bruinet Blazor Sample'.` then `Actual: 'Counter - Bruinet Blazor Sample'`.
- An assertion carries its locator only as text in the message, since `ControlCalls.Compare` builds it without one (`src/Bruinet.Core/Calls/ControlCalls.cs:327`). The failure JSON then reads it back with `Locator: (\S+)` (`UatStepFailure.cs:200`), which cuts a selector at its first space: `Css:[data-testid='login-form'] .text-danger` becomes `Css:[data-testid='login-form']`.
- That same pattern misses MAUI's lower-case "Child locator:" and "with locator:" (`src/Bruinet.Maui/Containers/ContainerObjectBase.cs:251`, `src/Bruinet.Maui/Context/MauiTestContext.cs:135`), so those failures have no locator in the JSON. Read, not run.
- `ScopeNotReadyException` has no locator in the JSON either, and a row that is gone is classified `timeout` (`UatStepFailure.cs:102`).
- A `Wait…` that runs out answers false and throws nothing. `WaitText` returned `False` after 1 s, and the last value seen is only in the log.

### 7. The stacks that were read

- WPF, WinForms, native Android and Stride build their failures through the same Core code as Html, so findings 1 and 2 hold for them by reading.
- FlaUI refuses an unsupported locator strategy without the suggestion the exception was made for (`src/Bruinet.FlaUI/LocatorExtensions.cs:67`). Native Android gives one (`NativeAndroidByExtensions.cs:25`).
- An app that shows no window fails with `Failed to get main window` (`src/Bruinet.FlaUI/DesktopApp.cs:88`). It names neither the process nor the 30 s it waited.
- Stride's start fails with `Failed to connect to pipe '…' within …ms` as a plain `TimeoutException` (`src/Bruinet.Stride/Communication/NamedPipeChannel.cs:54`), not as `AppUnavailableException`.
- The messages for things a platform cannot do are good: "A WPF app has no history to go back in; navigate with its page objects' controls." (`src/Bruinet.Wpf/Context/WpfTestContext.cs:106`).

## What is good and should stay

- The scope sentence: scope, call, control, budget, state and reason.
- The assertion's `Actual:` line, which tells null, empty and a value apart.
- The bind errors: file, line, code, the step, and the names that do exist.
- The replay misses: the nearest recorded exchange and exactly where it differs.
- A locator's strategy in the message. `AutomationId='nav'` showed at once that `"nav"` had not been read as a CSS selector.

## Proposals

| # | Proposal | Fixes |
| --- | --- | --- |
| 1 | Write "not found" as the scope sentence: `Click on control '<locator>' of page 'CounterPage': not found within 2000 ms.` One format for a locator everywhere. | Finding 2 |
| 2 | Give a control its member name, taken with `[CallerMemberName]` in the constructor, and put it in every message and the log. | Finding 1 |
| 3 | Print the catalog only for a bind error. For a run-time failure print the failed step, its message, and the paths of the trace and the result. | Finding 3 |
| 4 | Write a result for a scenario that failed to bind, with kind `bind`, so `failing`, `last_runs` and `get_trace` show it. | Finding 4 |
| 5 | For UATB001 name the nearest phrases, three at most, before the catalog. | Finding 4 |
| 6 | Add one closing line per kind of failure, saying what to try: a not-found names the page's controls that are shown, a scope that is not ready names the url or the root it waited for. | Outcome |
| 7 | Map a navigation that fails to one typed exception on both Html heads, and make bUnit refuse a host that is not its own. Then delete `PageLoadException` or throw it. | Finding 5 |
| 8 | Carry the locator on `ControlAssertionException` and `ScopeNotReadyException`, and drop the regex in `UatStepFailure`. | Finding 6 |
| 9 | Fix `IsFramework` so `Bruinet.Uat` is framework, and let `frames` name the page object member. | Finding 1 |
| 10 | Tidy the wording: one "disabled", one actual in `AssertTitle`, a detail for `NotLoaded` and `MissingRoot` on Html and MAUI, the process and time in "Failed to get main window". | Findings 5, 6, 7 |

Proposals 1, 3 and 4 give the most for the least: each is one place in the code.

## Unknown

- `why` was not called: it needs a green run of the same scenario before the red one, and the probe scenarios never had one.
- The FlaUI stacks, Appium, native Android and Stride were judged from source only. Their platform errors (UIA, WebDriver) were not seen as a user sees them.
- A hidden control, a control under an overlay and a frame that never loads were not provoked.
- A truly stale element was not seen as a final message on any head.
- SQL and HTTP misses were taken from the packages' own tests, not from a UI scenario where the app may catch the miss first.
- Whether `evidencePath` is null because the Html fixture offers no screenshot service was not followed further than `UatScenarioTestBase.cs:84`.

## Next action

Decide on proposals 1, 3 and 4. Each changes message text that consumers' logs and the failure JSON carry, so each needs its row in the api-upgrade change list.
