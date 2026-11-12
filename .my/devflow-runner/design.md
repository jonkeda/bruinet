# MauiDevFlow: a third MAUI device runner

Status: **built for Windows; Android launches and runs**, 2026-11-12. iOS attaches to a running app; its launch, and
Mac Catalyst, are not built. Decision: [AD-037](../../.docs/decisions/ad-037-a-maui-runner-is-chosen-apart-from-the-platform.md).
Run it: [docs/run/MAUI.md](../../docs/run/MAUI.md#devflow-runner-experimental).

## Where it stands

On Windows, the sample app's suite without `Tests\Background` and `Tests\Diagnostics` (FlaUI's own):
**352 passed, 2 failed, 31 skipped of 385**; the Shell app's `Tests\Shell`: **15 of 15**. `ScrollToEnd_LoadsEveryPage`
fails now and then in a full Collection run (one in four after the last fix) and passes alone: noted, not chased. Every failure is in the list below. A folder takes about as
long as on FlaUI (Text 17 s, Contracts 84 s).

### Android

On an Android 16 emulator (`Brinell_Android`, headless), the same suite: **327 passed of 385**. The runner installs the
APK, starts its launcher activity with the launch settings as string extras, sets `debug.bruinet.bridge` for the
bridge, and forwards a free port here to the agent's 9223 on the device (`AndroidLaunch`). The bridge's run-time ask is
that system property on Android (an app is given no environment variables): `AppSupport` reads it with `getprop` into
`BRUINET_UIA_BRIDGE` before the gate is read, decided by the owner on 2026-11-12. An Android alert, which the agent's tree does not show, is read from a
`uiautomator dump` into the Windows dialog shape and tapped or typed into over adb (`AndroidDialogs`), touch input as
the Appium runner's is, also the owner's call that day; the dump is taken only when the app's `CurrentAlert` says an
alert is open. What still fails, by cause:

| Cause | Tests | What it needs |
| --- | --- | --- |
| MediaElement | 11 | On Android the control asks for Media3's ids (`exo_*`), which have no stand-ins yet. |
| Rows and the carousel | 10 | Fewer rows realized on a phone screen, and the carousel shows one card: not yet worked out. |
| Date pickers | 6 | The date is read in a format Android does not show, and its focus is not reported. |
| A modal over a page | 1 | Android leaves the covered page out; not yet looked at. |

### What it cannot do

| Gap | Tests | Why |
| --- | --- | --- |
| A list item by its index (`ScrollToItem`, `Item(i)` past the realized rows) | 1 | The agent gives no row its index (`PositionInSet`). |
| Selecting a list row that has no tap of its own | 1 | The agent sets no `SelectedItem` (an object) and has no select action; the bridge's `SelectIndex` serves pickers only. |
| Scrolling a ScrollView inside another to an exact position | 0 | The agent spends an amount on the outermost ScrollView; the inner one reaches the top and the end exactly, a position between as near as its children allow. |


## How it was built, against this design

- **Raw tree, matched here:** one tree read per lookup, matched by `DevFlowLocators`, not the agent's own queries: its
  text match is a substring, and its tree keeps covered pages and scrolled-away elements, which `DevFlowTree` leaves
  out as the platform trees do. A read is kept 100 ms and dropped at every action.
- **Native nodes only for dialogs:** on Windows an alert is a native window with WinUI's ids (`PrimaryButton`); a read
  with native nodes costs four times a plain one (63 ms against 16 ms), so it is made when a dialog is asked for, and
  while one is open.
- **No stale-tree guard:** actions go without `captureEpoch`; the agent refused a tap after any change elsewhere.
- **Launch through `DesktopApp`:** on Windows the project also targets `net10.0-windows` and launches with Bruinet.FlaUI's
  `DesktopApp`, so the app stays behind the person's window; a free port per launch in `BRUINET_DEVFLOW_PORT`.
- **Stand-ins as on the headless head:** a Stepper's two buttons, an Expander's tap (`IsExpanded`), a carousel's swipe
  (`Position`), a pull-to-refresh (`IsRefreshing`), and the state names the bridge answers (`ItemCount`, `Date`, `Time`).
- **Scroll into view:** every scroller around the element, inside out; the agent's scroll-to-element first, the overlap
  as an amount where that moves nothing.
- **Lists:** a list's rows are put in reading order by their window bounds (the agent gives the platform's container
  order) and a container parked outside the stack of rows is left out (it still shows a removed item). A step toward an
  index puts the item at the top (`itemIndex`, `Start`) and is reported as a step, not a jump: with no row index the
  collection's next index is the count of rows seen. An element in a row is tied to what its row showed when found; a
  recycled container makes it stale, as a UI Automation row belongs to its item.
- **Nested ScrollViews:** a ScrollView with none around it is scrolled by the distance. One inside another is moved
  by showing a child at the start, centre or end of its nearest ScrollView (the agent's scroll-to-element), the stop
  nearest the target on the side it lies: exact at the top and the end, near between them.
- **Shell:** the agent reports Shell's structure (flyout items, tabs, sections, contents) as invisible and disabled;
  under it only the page with bounds is on screen (a tab left keeps its page with no height), and the flyout item's
  template beside it is left out. The agent's own `Tab` and `FlyoutItem` nodes, directly under the Shell, are
  `ShellChrome`'s tabs and flyout items (the host is the Shell itself, XPath `.`).
- **Bruinet's bridge over the agent:** what the agent has no route for goes through the verbs the app declares
  (`uia:GestureAutomation.Verbs`), as on Windows. `Bruinet.Maui.AppSupport` keeps the published elements and their
  plans in a registry beside UI Automation, and, in a build with `-p:BruinetDevFlow=true`, adds a DevFlow agent
  extension (`options.AddBruinetBridge()`, namespace `dev.bruinet.bridge`, the reverse-domain form the agent requires)
  with `invoke`, `exchange` and `declares` routes that run `MauiVerbDispatcher`. Behind the bridge's two gates (Debug,
  `BRUINET_UIA_BRIDGE=1`, which the runner sets). It carries menu items by AutomationId (context menus too), a picker's
  items and `SelectByText`, `Unfocus`, the app's gesture sinks (double-tap, pan), `GetState`, and `IsIdle`, which the
  runner asks after each action as the FlaUI runner settles after a write. Not a separate project as first proposed:
  a conditional reference in `AppSupport`, as the sample apps carry the agent, keeps it free of the preview package.
- **MediaElement:** its transport controls, which the platform draws outside the agent's tree, stand in under WinUI's
  ids (`DevFlowMediaPart`, found when a lookup by one of those ids finds nothing): play/pause named "Pause" while
  playing, the time readouts as `h:mm:ss`, the progress slider in seconds, all read from the element's `CurrentState`,
  `Position` and `Duration`. Play, pause and seek go through the bridge extension's `media` route, which calls the
  element's methods by name (the agent calls none, and `AppSupport` takes no reference to the toolkit).
- **Rows, again:** an element found inside a row inherits the row's item key, so a label read after its container was
  recycled is stale too, not the next item's text.
- **Tests:** `DevFlowRunnerTests` (27, fake agent); a `[WindowsOnlyFact]` about the bridge or the accessibility tree
  skips on this runner.

## Outcome

`Bruinet.Maui.DevFlow` is a third device implementation of `IMauiDriver` and `IMauiElement`, beside
`Bruinet.Maui.FlaUI` (Windows) and `Bruinet.Maui.Appium` (Android, iOS). It drives the app through the
DevFlow agent that runs inside the app over HTTP. Page objects, controls, the call model and feature files
run on it unchanged; only the fixture's choice of runner differs.

What it buys:

- **One runner on every platform:** Windows, Android, iOS simulator and Mac Catalyst. Bruinet has no Mac
  Catalyst runner today.
- **No Appium server, no WinAppDriver and no UI Automation quirks:** element facts come from MAUI's own
  visual tree (`AutomationId`, type, text, bounds, enabled and visible) in one read.
- **Speed:** one HTTP call to localhost per find or action. It skips UiAutomator2's idle waits and the
  XCUITest quiescence traps that `MauiDriverFactory.SessionDefaults` works around.

What it costs:

- **The app must carry the agent,** a debug-only package that cannot run in a shipping build.
- **Input is semantic, not physical:** `fill` sets `Text`, and `tap` invokes the element. A bug in the
  platform's input path goes unseen. This is the same limit AD-032 names for headless, at a smaller size.
- **The packages are previews** in `dotnet/maui-labs`: `0.1.0-preview.14.26555.2`.

## What MauiDevFlow is

| Piece | Package | Role |
| --- | --- | --- |
| Agent | `Microsoft.Maui.DevFlow.Agent` | `builder.AddMauiDevFlowAgent()` in the app's `MauiProgram`. Serves HTTP on loopback, at `http://localhost:9223/api/v1` by default. |
| Client | `Microsoft.Maui.DevFlow.Client` | `AgentClient(host, port)` covers the API and adds the mutation lease header. Targets netstandard2.0 and net8.0. |
| Driver | `Microsoft.Maui.DevFlow.Driver` | `AppDriverFactory`/`IAppDriver` launch the app per platform and forward ports (adb on Android). Targets net9.0. |
| CLI | `Microsoft.Maui.Cli` (`maui devflow`) | Runs the broker on 19223, which gives each agent a port from 10223-10899 and lists agents at `/api/agents`. |

The project was `Redth/MauiDevFlow`, which is now archived. The source sits in `dotnet/maui-labs/src/DevFlow`.

Four API facts shape the driver:

- **A mutation lease:** every POST, PUT or DELETE needs `X-DevFlow-Lease`. Claim it with
  `POST /agent/lease {action:"claim"}` and heartbeat it within 10 s, or the call answers 409.
- **No wait endpoint.** Bruinet polls itself (AD-004), as it already does.
- **Element ids are opaque and go stale.** Each element carries `captureEpoch` and `registryGeneration`.
  Actions accept them and refuse a stale tree.
- **Loopback only.** Windows, Mac Catalyst and the iOS simulator reach it directly. Android needs
  `adb forward`.

## Choosing the runner

Today the platform picks the driver: Windows uses FlaUI, and Android and iOS use Appium
(`MauiDriverFactory.Create`). DevFlow runs on all of them, so the runner becomes a choice of its own.

- `MauiDriverOptions.Runner`: `Default | DevFlow`. `Default` keeps today's mapping.
- Environment variable `BRUINET_MAUI_RUNNER=devflow`, read by `MauiTestFixtureBase` in the same place as
  `BRUINET_HEAD`.
- `MauiPlatform` gains `MacCatalyst`, which only DevFlow serves; `Default` there throws.
- `MauiDriverFactory` loads `Bruinet.Maui.DevFlow.DevFlowMauiDriver` by reflection, as it does for the
  other two, so `Bruinet.Maui` takes no reference to the preview packages.
- `MauiHead` stays `Device | Headless`, because DevFlow runs the real built app on a device. A runner is
  not a head (AD-020).

## The project

`src/Bruinet.Maui.DevFlow` (net10.0) references `Bruinet.Maui` and `Microsoft.Maui.DevFlow.Client`. It
references `Microsoft.Maui.DevFlow.Driver` for launching. Versions go in `Directory.Packages.props`.
AD-026's test: it earns its place, because it is a dependency only DevFlow users should pay for.

| File | Does |
| --- | --- |
| `DevFlowMauiDriver.cs` | `IMauiDriver`: launch or attach, lease, find, navigate, screenshot and quit. |
| `DevFlowMauiElement.cs` | `IMauiElement` over one `ElementInfo` snapshot, refreshed by id. |
| `DevFlowLease.cs` | Claims the lease, heartbeats it every 3 s on a timer, and releases it on `Quit`. |
| `DevFlowLocators.cs` | Turns a `Locator` into a query. |
| `LaunchedApp.cs` | Launches through `DesktopApp` on Windows (a plain process elsewhere), with a free port; the driver attaches to a running agent by port or finds it through the broker. |
| `DevFlowRefusals.cs` | Lists what the runner cannot do. Each throws an `IEndsCall` exception, as `DeviceOnlyException` does. |

## Mapping

### Driver

| `IMauiDriver` | DevFlow |
| --- | --- |
| `FindElements(locator)` | `GET /ui/elements?automationId=\|type=\|text=` or `?selector=` |
| `AppElement` | the root of `GET /ui/tree?depth=1` |
| `NavigateTo(route)` | `POST /ui/actions/navigate {route}` (Shell `GoToAsync`) |
| `NavigateBack()` | `POST /ui/actions/back` |
| `CurrentRoute()`, `NavigationDepth()` | `route` from `GET /agent/status`; depth read from the tree |
| `GetScreenshot()`, `TakeScreenshot()` | `GET /ui/screenshot` (PNG) |
| `InvokeMenuItem(id)` | `POST /ui/menus/invoke` |
| `ResetAppState()` | relaunch through `IAppDriver` |
| `FoundMeansVisible` | `false`; read `isVisible` instead |
| `OmitsOffscreenElements` | `false`; the tree holds every element |
| `Platform` | from `device.platform` in the status call |

### Element

| `IMauiElement` | DevFlow |
| --- | --- |
| `AutomationId`, `Text`, `TagName`, `Enabled`, `Visible`, `Focused`, `Selected`, `Rect` | `automationId`, `text`, `type`, `isEnabled`, `isVisible`, `isFocused`, `state.selected`, `bounds` |
| `Value`, `Checked`, `RangeValue/Minimum/Maximum` | `value`, or `GET /ui/elements/{id}/properties/{name}` (`IsChecked`, `IsToggled`, `Value`, `Minimum`, `Maximum`) |
| `Click()`, `Invoke()` | `POST /ui/actions/tap {elementId, captureEpoch, registryGeneration}` |
| `SendKeys`, `AppendText` | `POST /ui/actions/fill`. `TextInputMethod.Keys` maps to `/ui/actions/key` per character. |
| `Clear()`, `Focus()` | `/ui/actions/clear`, `/ui/actions/focus` |
| `SetChecked`, `SetRangeValue`, `SetDate`, `SetTime`, `SelectIndex` | `PUT /ui/elements/{id}/properties/{name}` |
| `ScrollIntoView`, `ScrollToIndex`, `ScrollTo` | `POST /ui/actions/scroll {elementId, itemIndex}` |
| `Swipe`, `LongPress`, `DoubleClick`, `PerformGesture` | `POST /ui/actions/gesture`, where it has the gesture; otherwise refused |
| `ReadState(property)` | `GET /ui/elements/{id}/properties/{property}` |
| `InstanceKey` | `id` |
| `RightClick`, `Hover`, `AnswerFileDialog`, native alerts | refused, pending the spike |

### Locators

| `Locator` | Query |
| --- | --- |
| `ByAutomationId`, `ByAccessibilityId`, `ById` | `automationId=` |
| `ByText`, `ByName` | `text=` |
| `ByClassName`, `ByControlType`, `ByTagName` | `type=` |
| `ByControlTypeAndName` | `type=` and `text=` |
| `ByCss` | `selector=` |
| `ByXPath` and the HTML-only kinds | refused |

## The sample apps

`samples/Bruinet.Samples.Maui.App/MauiProgram.cs` already calls `AddMauiDevFlowAgent()` under
`#if MAUI_DEVFLOW`. No csproj defines that symbol or references the package, so the code never compiles.

- The App and ShellApp projects get `<PackageReference Include="Microsoft.Maui.DevFlow.Agent">` when
  `$(BruinetDevFlow)=='true'`, and that property also defines `MAUI_DEVFLOW`.
- The default build stays free of the agent, so the FlaUI and Appium runs measure the app as shipped.

## Tests

- `tests/Bruinet.Maui.Tests`: unit tests for the locator mapping, the lease timer and the refusals,
  against a fake `HttpMessageHandler`.
- `tests/Bruinet.Maui.UITests` on Windows with `BRUINET_MAUI_RUNNER=devflow`: every test it fails is
  either a refusal (`[DeviceFact]`-style skip with a reason) or a bug in the runner.
- The parity count goes into this file: the number green on FlaUI and the number green on DevFlow.

## Order

1. **Spike:** attach to the sample app on Windows, claim the lease, find a button by `AutomationId`, tap
   it and read a label. Measure how long one find takes against FlaUI.
2. Build the project, the factory switch and the unit tests.
3. Run the Windows UI suite on DevFlow and fix or refuse until it is green.
4. Run the Android suite: adb forward through `IAppDriver`, then compare times with Appium.
5. Mac Catalyst and iOS simulator, on the Mac.
6. Record an AD, "A runner is chosen apart from the platform", and add a `docs/run/maui-devflow.md`.

## Open questions, each with a proposal

- **Does DevFlow judge a gate?** Proposal: no. FlaUI and Appium stay the judges, because DevFlow input
  is semantic. DevFlow is the fast device runner for an agent's inner loop and the only Mac Catalyst
  runner.
- **Launch with DevFlow's Driver package, or with Bruinet's own launchers?** Proposal: DevFlow's
  `IAppDriver`, because port forwarding and the broker are its job. Bruinet's
  `AndroidInstalledBuild`/`IosInstalledBuild` reinstall check sits in front of it.
- **Preview packages in a library Bruinet ships?** Proposal: yes, but only in `Bruinet.Maui.DevFlow`,
  marked experimental in its README. Bump them with each maui-labs preview.
- **The lease when a person's `maui devflow` session is attached?** Proposal: claim without `force`, and
  fail at the start with the holder's label. Never take the lease from a person.
- **`TextInputMethod.Keys` through `/ui/actions/key`?** Proposal: yes, if the spike shows that key events
  reach an `Entry`'s `TextChanged` as typing does. If not, keys fall back to `fill`, and the fallback is
  noted in the refusals.
