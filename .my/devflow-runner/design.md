# MauiDevFlow: a third MAUI device runner

Status: **design**, 2026-11-12. Checked against the tree on `move` (1ec3e616). No code yet.

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
| `DevFlowLaunch.cs` | Launches through `AppDriverFactory`, or attaches to a running agent by port or through the broker. |
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
