# Bruinet: swallowed errors

Written 2026-11-11 at `b2ea4570`. Review 5 of `additional-reviews-proposal.md`.
Read from the source under `src/` and `tools/`; nothing was built or run, and nothing was changed.

## Outcome

The call model itself keeps AD-009: `Poller`, `Confirmer`, `ControlCall`, the step runner and the Playwright and bUnit heads record or rethrow every error they catch.
The failures that can disappear sit below it, in three places: the Stride client, the Windows and Appium element classes of MAUI, and the scraper's recording queue.
The pattern is the same each time: a catch written for "this element does not offer that" also takes every other error, and answers "absent", "false" or "try the next route".

The proposal's "about forty" was low. There are 176 broad catches, and 84 of them drop the error without a trace.
Most of those 84 are harmless; fourteen findings below are not.

## What was counted

A catch is broad here when it is a bare `catch`, a `catch (Exception)` with no filter, or one whose filter only excludes (`is not OutOfMemoryException`, `!IsElementGone(error)`).

| Kind | Count | Note |
| --- | --- | --- |
| `catch` blocks in all | 500 | 227 name a type, 273 take `Exception` or nothing |
| `Exception` with a list of types in the filter | 97 | Narrow; read only where a finding led there |
| Broad | 176 | 153 in `src`, 23 in `tools` |
| `when (true)` or another filter that always passes | 0 | |
| Fire-and-forget tasks (`_ = …Async()`, `_ = Task.Run`) | 39 | 20 in `src`, 19 in `tools` |
| `ContinueWith` | 2 | One logs the fault, one drops it (finding 6) |
| `async void` | 4 | All in Presenter (finding 14) |

## How the 176 sort

| What happens to the error | Count | Where most are |
| --- | --- | --- |
| Rethrown, as it is or as a Bruinet exception | 28 | `AppiumErrors.Live`, `UiaElements.Live`, bUnit, clean-up-then-`throw` |
| Carried as a result the caller reports | 53 | Failed step, MCP tool error, runner answer, Presenter status line |
| Logged and dropped | 11 | Screenshots, the fixture restart, `AfterScenario` after a failure |
| Silently dropped | 84 | `FlaUIMauiElement` 24, Appium 16, `BruinetBridgeLookup` 10 |

The 28 and the 53 are right as they stand. A failed step keeps its exception (`BddExecution.cs:317`), a hook's failure fails the scenario (`BddHooks.cs:174`), and `Poller.cs:49` records what each attempt raised.

## The harmless ones

95 catches drop the error, logged or not. 38 of them are in the findings below; the other 57 change no test outcome and are counted here, not listed.

| Kind | Count | Why it is safe |
| --- | --- | --- |
| Diagnostics for a failure already reported | 15 | Tree dumps, bridge descriptions, placement report, page state |
| Logged and dropped | 11 | Screenshots, marks, the fixture restart, the Stride listener |
| Scroll and focus helpers | 11 | The caller's poll decides whether the element came into view |
| Bridge lookups and COM boundaries | 9 | They answer "not found", and the verb then fails saying so |
| Courtesy to the desktop or the next test | 5 | `QuietWindow`, closing a picker left open |
| Shutdown and dispose | 3 | `MauiTestContext.cs:252`, `StrideGameDriver.cs:58` and `:75` |
| Tooling fallbacks | 3 | Presenter settings, the code formatter, warm start |

Two of the logged ones would still read better louder: `ScreenshotService.cs:154` and `UiFact.cs:162` write to a logger or `Debug.WriteLine`, so a run whose every screenshot fails says so nowhere a person looks.

## Findings: a real failure can disappear

### 1. Stride reads a failed query as "absent"

- `StrideAutomationDriver.cs:91` returns null when `response.Success` is false, and lines 33, 36, 40 and 43 turn null into `Missing(target)` or an empty list.
- The game answers a missing element with `Ok` and `Exists = false` (`StrideUIHandler.cs:252`), so `Fail` on a query is always an error: an exception in the game's handler (`StrideUIHandler.cs:53`) or while the command is handled (`AutomationServer.cs:201`).
- The test sees an element that does not exist. `AssertExists(false)` and `AssertVisible(false)` pass on a game that threw.
- `IsTrue` at line 123 does the same for `IsGameBusy`: a failed busy query reads "not busy", and the call goes on without waiting.
- `TakeScreenshot` at line 71 returns an empty array for a failed capture.
- **Should take:** nothing; `Read<T>` should throw for `!response.Success`, as `Act` does at line 51, and keep null for `Result is null` only.

### 2. A write that threw is done again by another route

- `FlaUIMauiElement.cs:1375` turns any error from `pattern.SetValue(text)` into false.
- The caller at lines 519 to 524 then sends `SetText` over the bridge.
- AD-009's first rule is that an action that may have run is never repeated. A `SetValue` that threw after the app took the text is written twice, and the second write can pass.
- **Should take:** FlaUI's `PatternNotSupportedException` and `MethodNotSupportedException` (the route is not there); everything else ends the call.

### 3. Windows reads answer null or false for any error

- 14 property reads in `FlaUIMauiElement.cs` catch `Exception` unless the element is gone: `Selected` (160), `AutomationId` (173), `Value` (211), `IsReadOnly` (224), `Hint` (239), `Name` (286), `Text` (326), `GetAttribute` (878), `Checked` (1023), the range reads (1147), the selected item (1235) and three helpers (139, 197, 1335).
- `FlaUIErrors.IsElementGone` knows one HRESULT. A UIA timeout or an RPC error is none of it.
- The test sees null text or `Selected == false`. A positive assertion still fails, with a misleading "expected X, was null"; a negative one (`AssertSelected(false)`, `AssertText` not equal) passes.
- AD-009 says an error is never treated as absence.
- **Should take:** FlaUI's `PropertyNotSupportedException` and `PatternNotSupportedException`.

### 4. A Windows action that threw loses its reason

- `RunTogglePattern` (1053), `RunInvokePattern` (1085) and `RunSelectionItemPattern` (1101) return false for any error.
- `Perform` at line 455 then throws "was available but refused", with no inner exception.
- The failure stays a failure, so nothing disappears; what is lost is whether the app, UI Automation or Bruinet refused.
- **Should take:** no catch; let `Live` translate, or pass the error as the inner exception.

### 5. Appium reads and scrolls answer "no" for any error

- `AppiumMauiElement.cs:971` (`Checked`) and `:1002` (`IsAttributeTrue`) answer null and false; `:686` (`IsDisplayedNow`) answers false.
- `AppiumMauiDriver.cs:314` (`IsWhollyInView`), `:845`, `:976`, `:1033` and `:1048` answer false, null or an empty signature for a failed scroll or lookup.
- The filter `!AppiumErrors.IsGone(error)` lets a lost session through, which is the important half. It still takes a `NullReferenceException` in Bruinet's own code.
- `AppiumMauiDriver.cs:1118` and `:1149` are bare: they also take `AppUnavailableException` from the `FindElements` they call. The next call reports the lost session, one step late.
- **Should take:** `WebDriverException` with the same `IsGone` filter; at 1118 and 1149, `WebDriverException` and `StaleElementException`.

### 6. A recorded action that could not be saved leaves no sign

- `ScraperService.cs:850` chains each save with `ContinueWith(_ => next())`, which runs after a faulted task and never looks at the fault. `_chain` is awaited nowhere.
- `SaveActionAsync` (line 769) catches `ArgumentException` only. An `IOException` writing the session, or a `PlaywrightException` from `ShowCountAsync` at line 810, faults the task and is gone.
- The person recording sees nothing; the recording is short one action, and the drafted test is wrong.
- **Should take:** in `SaveActionAsync`, `IOException`, `UnauthorizedAccessException` and `PlaywrightException`, reported through `Changed` as `AssertNowSafeAsync` does at line 755; `Then` should observe faults.

### 7. `UatControlAssertions` turns every error into "not there"

- `UatControlAssertions.cs:27`, `:44`, `:56` and `:68` are bare catches around `WaitVisible`, `Exists`, `IsVisible` and `GetText`.
- `AssertAbsentOrHidden` (line 12) passes when `Exists()` throws, whatever it throws: a lost session, a wrong locator kind, a missing method on the control.
- The class is public and nothing in `src`, `tools` or `tests` calls it.
- **Should take:** `ElementNotFoundException` and `StaleElementException`, or the class should go.

### 8. The in-app bridge answers `E_FAIL` with the message lost

- `BridgeTargetProvider.cs:189` and `:249` catch whatever the app's target throws while it performs a verb; `BruinetAutomationPatternHandler.cs:57` does so for the whole dispatch.
- Catching is right: this is a COM boundary inside the app. What is lost is the exception, so the test reads "none answered" (`BridgeVerbRunner.cs:447`) and cannot say the app threw.
- Where the bridge is one of two routes (`FlaUIMauiElement.cs:519` to `:524`), a verb the app threw on reads the same as a route that is not there.
- **Should take:** the same catch, but the message kept where the client can read it (a last-error verb, or the app's own log).

### 9. A lost session reported as a missing item

- `AppiumMauiElement.Wheels.cs:64` catches `WebDriverException` from `wheel.SendKeys` and throws `InvalidOperationException` "has no item with text".
- `WebDriverException` is the base of every Selenium error, an ended session included, so `AppiumErrors.Live` above it no longer sees one.
- The test fails, with the wrong reason, and a fixture that restarts on `AppUnavailableException` does not.
- **Should take:** `InvalidElementStateException`, or the same catch with `when (!AppiumErrors.IsGone(error))`.

### 10. Putting the old date back may not happen

- `AppiumMauiElement.Wheels.cs:286` (`TrySend`) drops any `WebDriverException` with no comment; line 191 uses it to restore the wheels before refusing a date.
- The call still throws, so the test fails. The app may be left on a date nobody asked for, which the next test meets.
- **Should take:** the same type, with `IsGone` let through and the failure added to the message thrown at line 195.

### 11. A scenario's report can vanish

- `UatScenarioResults.cs:81` catches every exception from writing the report and the run's entry, and returns null.
- The test's own result is unchanged. The tools that read a run's results find the previous run's files, and an agent can take them for this one.
- A bug in `UatScenarioReport.Build` is taken the same way as a full disk.
- **Should take:** `IOException` and `UnauthorizedAccessException`, with a trace line; the rest should fail the test.

### 12. A recorded column type guessed on any error

- `ProviderRules.cs:203` answers `SqlBytes` when `reader.GetFieldType` throws anything.
- The comment-free catch is meant for a UDT whose CLR type is not loaded. Any other provider error records a wrong column type, and the replay then differs from the database.
- **Should take:** `FileNotFoundException`, `TypeLoadException` and `InvalidCastException`; this one I could not confirm against SqlClient without running it.

### 13. A modal pop nobody waits for

- `MauiHeadless/Interaction/Interactions.cs:493` discards `PopModalAsync()`.
- An exception the app raises while the page leaves (`OnDisappearing`, a `Popped` handler) ends up in a task nobody observes.
- `TestDispatcher.cs:322` promises that an `async void` handler's exception fails the test; this is a `Task`, so that net does not hold it.
- **Should take:** no catch; the task should be drained and its exception thrown, as `TestDispatcher.cs:342` does for a posted callback.

### 14. Smaller ones in the tools

- `TemplateWriter.cs:22` drops a missing embedded template; the generator then writes an empty file and reports success. Should take nothing.
- `AsyncRelayCommand.cs:24` is `async void` with `try`/`finally` and no catch, as are `RunUntil` and `RunFrom` (`PresenterShellViewModel.Run.cs:423`, `:437`): a command that throws takes Presenter down. Should catch and show the message, as `DebugFrom` at line 462 does.
- `BddLanguageServer.Step.cs:290` discards the runner's answer to `BddRunnerBreak`; a runner that fails it leaves the IDE waiting for a break that never comes.
- `UatScenarioCheck.cs:67` lists `NullReferenceException` in its filter, so a bug in a host's `ModulesFor` reads as "needs a started fixture". Should take `NotSupportedException` and `InvalidOperationException`.
- `MockAudioSource.cs:45` discards the emit loop; an exception in the app's `ChunkEmitted` handler stops the audio and reaches no one.

## One cause under several findings

`InvalidOperationException` is Bruinet's word for "the platform refused": `FlaUIMauiElement.cs:455`, `StrideAutomationDriver.cs:51`, `AppiumMauiElement.Wheels.cs:66`.
It is also what .NET throws for a great many bugs.
So `CollectionObjectBase.cs:1004` and `:1354`, which catch it to mean "this candidate refused", cannot tell a refusal from a fault.
A typed refusal would let those catches, and findings 2 to 5, name what they mean.

## Proposals

| # | Proposal | Findings |
| --- | --- | --- |
| 1 | Stride's `Read<T>` and `IsTrue` throw on a failed response; null stays for "no result". | 1 |
| 2 | In `FlaUIMauiElement`, replace `catch (Exception) when (!IsElementGone)` by FlaUI's not-supported exceptions: on the 14 reads, the 4 action helpers and `TrySetTextValue`. | 2, 3, 4 |
| 3 | In the Appium classes, catch `WebDriverException` with the `IsGone` filter; remove the two bare catches. | 5, 9, 10 |
| 4 | The scraper's queue observes faults and reports them through `Changed`. | 6 |
| 5 | Delete `UatControlAssertions`, or narrow its four catches. My proposal: delete, since nothing calls it. | 7 |
| 6 | The bridge keeps the app's exception message for the client to read. | 8 |
| 7 | `UatScenarioResults.Record` catches file errors only and traces them. | 11 |
| 8 | A typed "refused" exception in Core in place of `InvalidOperationException`. | The cause above |
| 9 | A check for new ones: an analyzer rule or a `check-*.ps1` that fails a new bare catch or unfiltered `catch (Exception)` outside an allowed list. | All |
| 10 | The small ones, each a line or two. | 12, 13, 14 |

Proposals 1, 2 and 3 are the ones AD-009 asks for; 9 keeps the count from growing back.

## Unknown

- Which exceptions UI Automation raises when the app's own handler throws during `SetValue`, `Invoke` or `Toggle`, or while the app is dying. Findings 2, 3 and 4 rest on reading, not on a run.
- What the app-side bridge targets do when the app's handler throws: they live outside `src/` and `tools/` and were not read.
- Whether `PopModalAsync` in `MauiHeadless` ever completes later than the call, and so whether finding 13 can occur.
- Whether any consumer uses `UatControlAssertions`; Tomorrow was not searched.
- The 97 catches with a type list and the 227 typed catches were not read one by one. Of the typed ones, the eight `catch (WebDriverException)` were read; findings 9 and 10 came from them.
- The 18 fire-and-forget calls in `tools/Bruinet.Cli/Lsp` other than the one in finding 14 were read at the call only, not into the method each starts.

## Next action

The owner picks from the proposals. My order: 1, then 2 and 3 together with a run of the MAUI contract tests on both heads, then 9.
