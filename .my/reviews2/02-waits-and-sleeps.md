# Waits and sleeps: review and proposals

Written 2026-11-11 at `b2ea4570`. Review 2 of `additional-reviews-proposal.md`.
Read from the source under `src/` and `tools/`; nothing was built or run.

## Outcome

AD-004 holds in the code that matters. Of the 46 `Task.Delay` and `Thread.Sleep` calls, one is a guess; 33 pause inside a wait for a condition and 12 are pacing, where the time is the product.
The call model has one sleep, `WaitHelper.Pause`, and three callers, all in `Bruinet.Core.Calls`.
The weak spots are elsewhere: nine hand-written wait loops in MAUI's two device heads, a start-up wait in Stride that gives up without saying so, and timeout settings that are set in five places and read nowhere.

The call budgets agree across the stacks, with three differences that are on purpose. They can be changed in one place.
The waits below a call and at start-up are constants; no setting and no scale reaches them.

## How the 46 sort

Measured by grep for `Task.Delay(` and `Thread.Sleep(` in `*.cs`: 40 in `src/`, 6 in `tools/`.

| Class | Count | What it is |
| --- | --- | --- |
| Poll, with a condition and a bound | 20 | The pause between two looks |
| Watcher, a poll until stopped | 3 | A background loop's period |
| Retry pause, bounded attempts | 8 | A file or service that was busy |
| Timeout arm | 2 | A delay raced against the real event |
| Pacing | 12 | Recording, replayed time, a held press, a rate limit |
| Guess | 1 | A fixed sleep hoping something finished |

The first four rows are the assignment's "poll" class: 33 together.

## The guess

- `src/Bruinet.Stride.Automation/StrideUIHandler.cs:601` sleeps 200 ms after `SetForegroundWindow`, then copies the window's pixels from the screen (`:539`, `:552`).
- Nothing checks that the window came forward or that the game drew a frame since.
- **Wait on instead:** `GetForegroundWindow() == hwnd`, then one more frame drawn by the game (the handler runs in the game, so it can count `Draw` calls), within a bound of about a second.
- Better still, read the game's back buffer, which needs no foreground at all; AD-024 says a recorder never activates the app, and this screenshot does.

Outside the 46, two more guesses stand in `tools/`:

- `tools/Scripts/run-android-tests.ps1:143` sleeps 5 s after starting Appium, then asks `/status` once. **Wait on instead:** poll `/status` until it answers, as `tools/Scripts/scrape-loop.ps1:145` already does for its app.
- `tools/Bruinet.Bdd.VsCode/src/test/suite.ts:118` sleeps `watcherSettleMs` after touching a feature file. **Wait on instead:** the language server's next diagnostics for that file, or retry the second Step-through until it starts.

## Polls that are right

Each has a condition, a deadline and an interval.

| Site | Waits for | Bound / interval |
| --- | --- | --- |
| `Core/Utilities/WaitHelper.cs:25` | The call model's one pause (`Poller.cs:72`, `Confirmer.cs:66`, `ControlCalls.cs:383`) | The call's budget / `PollingInterval` |
| `Maui/Containers/CollectionObjectBase.cs:1275` | A row the platform activates | The call's budget / `PollingInterval` |
| `Maui/Testing/NavigationRecovery.cs:143` | A known page or the root | 2000 or 300 ms / 50 ms |
| `Mocking/MockApiServer.cs:226`, `:262` | A request to arrive | 10 s / 25 ms |
| `Html/Testing/WebAppHost.cs:201` | The app to answer its URL | 60 s / 100 ms |
| `FlaUI/Windowing/AppWindow.cs:99` | UI Automation to resolve the handle | 10 s / 20 ms |
| `Mocking.Data/SqliteTemplate.cs:121` | The lock file | 2 min / 50 ms |
| `Uat/Runner/BddRunnerHost.cs:262` | A debugger to attach | The request's seconds / 100 ms |
| `tools/Bruinet.Bdd.VisualStudio/CopilotChatSender.cs:147` | An active chat session | 10 tries / 200 ms |

`CollectionObjectBase.cs:1275` calls `Thread.Sleep` itself where the other three call sites use `WaitHelper.Pause`; it is the same pause written twice.

## Polls below a call, written by hand

AD-004 says nothing below a call's poll sleeps or loops on its own. `Bruinet.Core.Calls.Settle.Until` is the allowed form: one confirmation, cut to the call's budget.
Nine loops in MAUI's device heads do the same job with their own `Thread.Sleep`. All nine cut their window with `CallBudget.Within`, so none outlasts the call, with one exception below.

| Site | Waits for | Window / interval |
| --- | --- | --- |
| `Maui.FlaUI/FlaUIMauiElement.cs:719` | The scroll percent to move | 500 / 25 ms |
| `Maui.FlaUI/FlaUIMauiElement.cs:1531` | Two scroll reads that agree | 2000 / 25 ms |
| `Maui.FlaUI/FlaUIMauiDriver.cs:204` | A chrome element to be found | Caller's / 100 ms |
| `Maui.FlaUI/FlaUIMauiDriver.cs:463` | The bridge to answer `GetState` | 2000 / 25 ms |
| `Maui.FlaUI/FlaUIMauiDriver.cs:524` | The back verb to be delivered | 1000 / 25 ms |
| `Maui.FlaUI/FlaUIMauiDriver.cs:786` | The file dialog to show | Caller's / 100 ms |
| `Maui.FlaUI/FlaUIMauiDriver.cs:808` | The file dialog to close | Caller's or 2 s more / 100 ms |
| `Maui.FlaUI/Bridge/BridgeVerbRunner.cs:345` | A page to publish its targets | 1000 / 25 ms |
| `Maui.Appium/AppiumMauiElement.cs:779` | Two position reads that agree | 500 / 50 ms |

- These waits are not in the call log, because only `Confirmer` and `Poller` write to it.
- `FlaUIMauiDriver.cs:803` sets the close deadline to `Math.Max(timeoutMs, elapsed + 2_000)`, so it can run up to 2 s past the call's budget.
- `FlaUIMauiElement.cs:1531` and `AppiumMauiElement.cs:779` sleep before the first comparison, so a control that is already still costs 25 or 50 ms on every call.

## Watchers, retries and timeout arms

None of these is a guess; each is listed so the count closes.

- **Watchers:** `FlaUI/Windowing/QuietWindow.cs:144` (5 ms, then 100 ms after 15 s), `tools/Bruinet.Bdd.VisualStudio/BddRunController.cs:656` (500 ms, while the run's process lives), `tools/Bruinet.Cli/Lsp/McpReloadingHost.cs:200` (2 s, `Poll` at `:41`).
- **File retries:** `Mocking/NetworkStateFile.cs:122` (20 × 10 ms), `Bdd/BddBreakpoints.cs:341` and `:363` (10 × 20 ms), `Presenter/Services/PresenterBreakpointStore.cs:86` and `:103` (20 ms), `tools/Bruinet.Bdd.VisualStudio/BddBreakpointStore.cs:194` (10 × 20 ms).
- **Service retries:** `Narrator/CloudVoice.cs:160` (the service's `Retry-After`, else 2 s), `Stride.Automation/AutomationServer.cs:157` (1 to 5 s after a listener error).
- **Timeout arms:** `Narrator.Windows/WindowsSpeech.cs:62` (the clip's length plus 1 s, against `MediaEnded`), `tools/Bruinet.Bdd.VisualStudio/CopilotChatSender.cs:170` (3 s, against the request's answer).
- The same breakpoint file is retried by three copies of the same loop, in `Bruinet.Bdd`, the Presenter and the Visual Studio extension.

## Pacing

AD-024's two seams are where it says, and neither runs without a recording.

- `Core/Recording/ActionPacing.cs:65`: the scene's pause after an action, outside the call's logged time.
- `Narrator/BddNarrationPacer.cs:134` and `:153`: the beat between steps and a line's length.
- `Recording/PacedVideo.cs:113`: the time until the next frame is due.
- `Presenter/ViewModels/PresenterShellViewModel.Run.cs:618`: the Presenter's chosen delay between steps.
- `Mocking.Http/HttpReplaySession.cs:199`, `:554`, `:607`: a recorded answer's own delay, only at `HttpReplayPace.Recorded`.
- `Mocking/Sensors/MockAudioSource.cs:83`: the interval between audio chunks, 100 ms by default.
- `FlaUI/UiaElementBase.cs:315`: how long a long press is held.
- `Scraper/Explore/Explorer.cs:279`: 500 ms between clicks "so the app is not hammered" (`ExploreSettings.cs:26`); the capture that follows settles by itself (`PlaywrightScrapeSource.cs:555`).
- `tools/Bruinet.Bdd.VisualStudio/PreviewController.cs:720`: 300 ms without typing before the preview refreshes.

`HttpReplaySession.cs:554` is the only one of the three replay delays that takes no cancellation token.

## Two waits that fail quietly or never

- `src/Bruinet.Stride/Infrastructure/StrideGameDriver.cs:103-115` polls for the game's main window until `StartupTimeout`, and when the time runs out the loop simply ends. The driver then tries the pipe, so a game that never showed a window is reported as a pipe that would not connect.
- `src/Bruinet.FlaUI/DesktopApp.cs:84` calls `process.WaitForInputIdle()` with no timeout. An app that hangs at start hangs the WPF, WinForms and MAUI Windows suites with it.

## Call budgets, per stack

One class holds them: `TimeoutSettings` on `TestContextOptionsBase.Timeouts` (`src/Bruinet.Core/Configuration/TestContextOptionsBase.cs:20`).
A control verb with no `timeoutMs` takes `DefaultWait`; a page's `WaitReady` and `Open` take `PageLoad`.

| Stack or head | Wait / page load / poll / animation (ms) | Set at |
| --- | --- | --- |
| Html on Playwright, Blazor | 5000 / 10000 / 100 / 300 | `Core/Configuration/TimeoutSettings.cs:38-64` |
| Html on bUnit | 5000 / 10000 / 10 / 300 | `Html.Bunit/BunitTestContextOptions.cs:15-18` |
| MAUI on Appium and FlaUI | 5000 / 10000 / 100 / 300 | `TimeoutSettings.cs:38-64` |
| MAUI on MauiHeadless | 5000 / 10000 / 25 / 0 | `Maui.Headless/MauiHeadlessHead.cs:18-26` |
| Native Android, WPF, WinForms | 5000 / 10000 / 100 / 300 | `TimeoutSettings.cs:38-64` |
| Stride | 10000 / 15000 / 100 / 150 | `Stride/Context/StrideTestContextOptions.cs:13-21` |

- The defaults agree. The three that differ each say why in their comment: the in-process heads poll faster, and a game starts slowly.
- A consumer changes them in one place, three ways: the fixture's options in code, the settings file read by `TestContextOptionsBase.Load<T>` (`:48`), or `BRUINET_TIMEOUT_SCALE` for the whole run (`TimeoutSettings.cs:14-32`).
- A test that needs longer passes its own `timeoutMs`, which is never scaled.

## Settings that do nothing or mislead

- `ElementFind` and `ElementState` are set in five places (`TimeoutSettings.cs:50`, `:56`, `:78-79`, `:91-92`, `StrideTestContextOptions.cs:17-18`, `MauiHeadlessHead.cs:22-23`) and read nowhere in `src/`, `tools/` or `tests/`. Stride's `ElementFind` of 1000 ms changes nothing.
- `Bruinet.Core.Configuration.TimeoutDefaults` (`TimeoutDefaults.cs:6-37`) is public, unused, and gives 5000 for both where `TimeoutSettings` gives 3000.
- `Animation` is documented as "Delay for animation settling" (`TimeoutSettings.cs:58`). It is the scroll throttle (`Core/Calls/ControlCall.cs:42`, `AttemptContext.cs:46`, `:62`); nothing sleeps for it.
- `NavigationRecovery`'s `postNavigationDelayMs` (`Maui/Testing/NavigationRecovery.cs:29`) is a wait's budget, not a delay.
- `WaitHelper.Pause(int)` is still a public sleep (`Core/Utilities/WaitHelper.cs:17`), as the public API review said.

## Waits below a call: fixed

Each is a constant in the driver. A fixture cannot change one, and `BRUINET_TIMEOUT_SCALE` does not reach them; the call's remaining budget only shortens them.

| Head | Wait | Window / poll (ms) and site |
| --- | --- | --- |
| Playwright | A plain `Is…` outside a call | 5000, `Html.Playwright/PlaywrightHtmlElement.cs:28` |
| Playwright | A read's wait for attachment | 250, `PlaywrightHtmlElement.cs:44` |
| Playwright | Page ready, in the page | Caller's / 10, `PlaywrightHtmlDriver.cs:64` |
| bUnit | An event's handler | 100 / 5, `Html.Bunit/BunitHtmlElement.cs:452-455` |
| MauiHeadless | The dispatcher quiet | 25, `Maui.Headless/MauiHeadlessDriver.cs:176` |
| FlaUI (MAUI) | Dropdown open and closed | 2000 / 50, `FlaUIMauiElement.cs:1177`, `:1303` |
| FlaUI (MAUI) | Shell chrome | 5000 / 100, `FlaUIMauiElement.cs:1840` |
| FlaUI (MAUI) | The nine loops above | 500 to 2000 / 25 to 100 |
| Appium (MAUI) | Shell chrome, picker items | 5000, `AppiumMauiElement.cs:1156`, `:1257` |
| Appium (MAUI) | Flyout open or closed | 5000 / 100, `AppiumMauiElement.cs:1321-1332` |
| Appium (MAUI) | Picker dialog | 3000 / 50, `AppiumMauiElement.Pickers.cs:33`, `:235` |
| Appium (MAUI) | A wheel landing | 1500 / 100, `AppiumMauiElement.Wheels.cs:34`, `:184` |
| Appium (Android) | UiAutomator's idle wait | 500, `NativeAndroid/Appium/AppiumNativeAndroidDriver.cs:23` |
| Stride | One command's answer | 10 s, `Stride/Communication/NamedPipeChannel.cs:63` |

The poll intervals below a call are 5, 10, 20, 25, 50 and 100 ms, each chosen where it stands. `Settle.Until` defaults to 50 (`Core/Calls/Settle.cs:15`).

## Waits outside a call

| Wait | Default and site | Can a consumer change it |
| --- | --- | --- |
| Stride start-up, pipe connect | 15 s, 10 s, `StrideTestContextOptions.cs:27`, `:31` | Yes, options |
| Web app start | 60 s / 100 ms, `Html/Testing/WebAppHost.cs:20`, `:36` | Timeout yes (`init`), poll no |
| Desktop app's main window | 30 s, `FlaUI/DesktopApp.cs:87`, `:113` | No |
| Window handed to UI Automation | 10 s / 20 ms, `FlaUI/Windowing/AppWindow.cs:84-85` | No |
| Mock API request | 10 s / 25 ms, `Mocking/MockApiServer.cs:38-41` | Per call (`timeout`), not the default |
| SQLite template lock | 2 min / 50 ms, `Mocking.Data/SqliteTemplate.cs:11-14` | No |
| MauiHeadless's own `WaitFor` | 2 s, `MauiHeadless/MauiTestContextOptions.cs:51` | Yes, options |
| Navigation recovery | 2000, 300 / 50 ms, `NavigationRecovery.cs:12`, `:28-29` | Budgets yes, poll no |
| Runner connect, cancel grace | 30 s, 5 s, `Bdd/Runner/BddRunnerClient.cs:88-91` | No |
| Recorder's first frame | 10 s, `Html.Playwright/PlaywrightRecorder.cs:51`, `Narrator.Windows/WindowsCaptureRecorder.cs:54` | No |
| ffmpeg input opened | 15 s, `Recording/FfmpegRegionRecorder.cs:41` | No |
| UAT page-state read | 200 ms, `Uat/UatPageState.cs:13` | No |

None of these is scaled by `BRUINET_TIMEOUT_SCALE`. A machine slow enough to need a scale of 2 on its call budgets still gets 30 s for a main window and 10 s for a mock request.

## Proposals

1. **Replace the Stride screenshot's sleep.** Wait for the foreground window and one drawn frame, or read the back buffer and drop `SetForegroundWindow` (`StrideUIHandler.cs:596-602`).
2. **Make Stride's start-up wait fail by name.** Throw a `TimeoutException` naming `StartupTimeout` when no window showed (`StrideGameDriver.cs:103-115`).
3. **Bound `WaitForInputIdle`.** Give it the main window's 30 s and fail with the app's name (`DesktopApp.cs:84`).
4. **Move the nine MAUI loops onto `Settle.Until`.** One form, one default interval, and the waits appear in the call log. The two "reads agree" loops need a `Settle` variant that compares with the previous look.
5. **Keep the file dialog's close inside the budget.** Drop the `+ 2_000` at `FlaUIMauiDriver.cs:803`, or say in the remark why this one wait may outlast its call.
6. **Delete what is not read.** `TimeoutDefaults`, and `ElementFind` and `ElementState` with their five initialisers. Nothing is shipped, so it is free now.
7. **Say what `Animation` is.** Fix its summary to the scroll throttle, or rename it `ScrollThrottle` while renames are free.
8. **Scale the fixed windows too.** Let `CallBudget.Within` multiply a window by `TimeoutSettings.Scale`, and do the same for the launch waits; the slow machine is where they bite.
9. **Give the launch waits a home.** A `StartTimeout` on the desktop stacks' options, as Stride and `WebAppHost` have, in place of the constants at `DesktopApp.cs:87`, `:113` and `AppWindow.cs:84`.
10. **Make `WaitHelper.Pause` internal.** Its three callers are in Core; have `CollectionObjectBase.cs:1275` use the poll's pause through `Poller` or keep its own sleep.
11. **One retry for the breakpoint file.** `Bruinet.Bdd`'s `WriteWhole` and `ReadWhole` used by the Presenter; whether the Visual Studio extension can share it depends on its target framework, which this review did not check.
12. **Poll in the two scripts and the VS Code test.** The conditions are named under "The guess".
13. **Add the check AD-004 asks for.** A banned-API rule for `Thread.Sleep` and `Task.Delay` in `src/`, with the pacing and poll files listed as allowed, so the next guess fails the build.

## Not settled

- Nothing was run, so whether any fixed window is too short on a device or a slow agent is not known; only that it cannot be changed.
- Whether the Stride screenshot path at `StrideUIHandler.cs:520-534` is still used now that AD-024's recorders exist was not traced.
- That Html, MAUI, Native Android, WPF and WinForms take `TimeoutSettings.Default` unchanged rests on a grep of their `*TestContextOptions.cs` files for `Timeouts`, which found only bUnit and Stride.
- `tests/` and the samples were out of scope; their sleeps are not counted here.
- The six `setTimeout` calls in `tools/Bruinet.Bdd.VsCode/src` and the four in `src/Bruinet.Scraper/Html/Scripts` were read only as far as this review names them.
