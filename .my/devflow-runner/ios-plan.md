# DevFlow runner on the iOS simulator: plan

Purpose: bring the DevFlow runner from "attaches to a running iOS app" to "launches the app and runs the
sample suite", one small test group at a time. Design and Windows and Android results:
[design.md](design.md). Status: steps 1-6 done, 2026-11-13. Every group passes on the iOS 26.0 simulator; the
results and the routes built are in [design.md](design.md#ios). Each group was run alone, not the whole suite at once.

## Inputs and current state

- `DevFlowMauiDriver.CanLaunch` launches only a Windows `.exe` and an Android `.apk`; iOS falls through to
  `Attach` or throws `PlatformNotSupportedException`.
- `ILaunchedApp` has two implementations: `DesktopLaunch` (Windows) and `AndroidLaunch` (adb). Android-only
  paths hang off `_app is AndroidLaunch`: alerts (`AndroidDialogs`), list swipes (`SwipesLists`,
  `SwipeList`), typing into an alert, and `DeviceCulture`.
- The bridge gate: `BruinetBridgeHost.Enable` copies Android's `debug.bruinet.bridge` into
  `BRUINET_UIA_BRIDGE`. iOS has no such step yet. It needs none if the launch passes the variable, see
  step 1.
- `Bruinet.Maui.AppSupport` already targets `net10.0-ios`, so the DevFlow bridge extension builds for iOS
  when `-p:BruinetDevFlow=true`.
- `IosInstalledBuild` (in `Bruinet.Maui`) already wraps `xcrun simctl` for the Appium runner's reinstall
  check, terminate and data reset. Reuse it; do not write a second simctl wrapper.
- This Mac: iOS 27.1 simulators (iPhone 17 Pro and others), all shut down; `Microsoft.iOS.Sdk.net10.0_27.1`
  installed.
- Tests run from `tests/Bruinet.Maui.UITests.Mobile` (links the Windows project's tests, leaves out
  `Tests/Background` and `Tests/Diagnostics`).

## Facts that make iOS simpler than Android

- **No port forwarding.** The simulator shares the Mac's loopback, so the agent on `localhost:<port>`
  answers directly, as on Windows.
- **Environment variables reach the app.** `xcrun simctl launch` passes every `SIMCTL_CHILD_<NAME>`
  variable to the app as `<NAME>`. `BRUINET_DEVFLOW_PORT`, `BRUINET_UIA_BRIDGE` and the launch settings
  travel the Windows way, with a free port per launch. No `setprop` stand-in is needed.

## What iOS lacks that Android had (to settle in the spike)

| Android route | iOS question | First proposal |
| --- | --- | --- |
| `uiautomator dump` for alerts | Does the agent's tree show a `UIAlertController`? | If not, a bridge route in `AppSupport` (iOS only) that reads the presented alert and taps an action or fills its text field in-process. |
| `adb input swipe` for paging lists | Does the agent's scroll move a `UICollectionView` and fire `RemainingItemsThreshold`? | Likely yes (it scrolls the native view). If not, a bridge `scroll` route that calls `SetContentOffset` with animation. simctl has no touch input. |
| `getprop` locale for `DeviceCulture` | Where is the simulator's locale? | `xcrun simctl spawn <udid> defaults read -g AppleLocale`. |
| MediaElement `exo_*` stand-ins | Which ids do iOS tests ask for? | Read the first failures; add stand-ins only for what the tests ask for. |

Each answer is an owner's call before it is built, as the Android touch and `getprop` calls were.

## Steps

Each step ends with a build of `src/Bruinet.Maui.DevFlow` and `DevFlowRunnerTests` green on `net10.0`.
Run one test group per command (see [Test groups](#test-groups)). Never run the whole suite in one go
until step 6.

1. **Build and attach (spike).** Build the sample app for the simulator with the agent. Boot one simulator,
   install, launch by hand with `SIMCTL_CHILD_BRUINET_DEVFLOW_PORT` and `SIMCTL_CHILD_BRUINET_UIA_BRIDGE=1`,
   attach with `BRUINET_DEVFLOW_PORT`, and run group 1. Record find and tap times. Answer the four spike
   questions above by probing the agent (`/ui/tree` with an alert open, a scroll on the Collection page).
2. **`IosLaunch : ILaunchedApp`.** It boots the simulator named by `APPIUM_UDID` (or the first booted one),
   installs the `.app` unless `IosInstalledBuild` says it is current, terminates any old process,
   and launches with `SIMCTL_CHILD_` variables and a free port. `ProcessId` comes from
   `simctl launch`'s output, and `HasExited` from `simctl spawn <udid> launchctl list`. `Dispose` terminates
   the app. Wire it into `CanLaunch` (`.app` directory) and `Launch`, and wait for the agent as Android does,
   matching by bundle id. Add fake-agent unit tests for the dispatch and the argument building only.
3. **Groups 1-4 green or explained.** Fix in the runner; for a real gap, add a refusal with its reason.
4. **Alerts and lists (groups 5-8).** Build what the spike chose for alerts and paging, behind
   `_app is IosLaunch`, beside the Android branches.
5. **Remaining groups (9-15).** Gestures, navigation, Shell, Community Toolkit, contracts.
6. **One full run, then write it down.** Run the suite once, add an "iOS" section to
   [design.md](design.md#where-it-stands) with counts and a cause table, and update
   [docs/run/MAUI.md](../../docs/run/MAUI.md#devflow-runner-experimental) with the iOS commands.

Mac Catalyst stays out of this plan.

## Test groups

The count is the number of test attributes in the folder, a rough figure. Run each group alone and record
passed, failed and skipped before moving on.

| Group | Filter (`FullyQualifiedName~`) | About | Step |
| --- | --- | --- | --- |
| 1 | `Tests.Buttons.` | 5 | 1 (spike) |
| 2 | `Tests.Text.` | 28 | 3 |
| 3 | `Tests.Toggle.` and `Tests.Selection.` | 19 | 3 |
| 4 | `Tests.Range.` and `Tests.Display.` | 29 | 3 |
| 5 | `Tests.Dialogs.` | 10 | 4 |
| 6 | `Tests.DateTimes.` | 14 | 4 |
| 7 | `Tests.Scroll.` | 7 | 4 |
| 8 | `Tests.Collection.` | 30 | 4 |
| 9 | `Tests.Container.` | 26 | 5 |
| 10 | `Tests.Gestures.` | 22 | 5 |
| 11 | `Tests.Navigation.` | 38 | 5 |
| 12 | `Tests.Contracts.` | 27 | 5 |
| 13 | `Tests.CommunityToolkit.` without MediaElement | about 30 | 5 |
| 14 | `Tests.CommunityToolkit.` MediaElement only | about 10 | 5 |
| 15 | `Tests.Shell.` (Shell app build) | 15 | 5 |

A failing group is rerun with a narrower filter (one class, then one test), not widened.

## Commands (macOS)

Build once per runner change to the app (not per group):

```bash
dotnet build samples/Bruinet.Samples.Maui.App -f net10.0-ios -c Debug \
  -p:RuntimeIdentifier=iossimulator-arm64 -p:BruinetDevFlow=true \
  -p:OutDir=$PWD/samples/Bruinet.Samples.Maui.App/bin/DevFlowIos/
```

Step 1 (attach, by hand):

```bash
UDID=DEA93461-CBC7-4866-95CA-5A599F0815FD   # iPhone 17 Pro, iOS 27.1
xcrun simctl boot $UDID 2>/dev/null; xcrun simctl install $UDID samples/Bruinet.Samples.Maui.App/bin/DevFlowIos/Bruinet.Samples.Maui.App.app
SIMCTL_CHILD_BRUINET_DEVFLOW_PORT=9223 SIMCTL_CHILD_BRUINET_UIA_BRIDGE=1 \
  xcrun simctl launch --terminate-running-process $UDID com.bruinet.samples.maui
BRUINET_MAUI_RUNNER=devflow APPIUM_PLATFORM=ios BRUINET_DEVFLOW_PORT=9223 \
  dotnet test tests/Bruinet.Maui.UITests.Mobile --filter "FullyQualifiedName~Tests.Buttons."
```

From step 2 on, the runner launches the app:

```bash
BRUINET_MAUI_RUNNER=devflow APPIUM_PLATFORM=ios APPIUM_UDID=$UDID \
APPIUM_APP_PATH=$PWD/samples/Bruinet.Samples.Maui.App/bin/DevFlowIos/Bruinet.Samples.Maui.App.app \
  dotnet test tests/Bruinet.Maui.UITests.Mobile --no-build --filter "FullyQualifiedName~Tests.Text."
```

## Unknowns

- Whether the agent package (`0.1.0-preview.14.26555.2`) runs on iOS 27.1 at all; the spike answers it.
- Whether `simctl launch` with a free port per launch leaves an old agent answering on its port after
  `--terminate-running-process`; the launch waits for the agent of the new process, as on Android.
- Whether `IsIdle` from the bridge settles iOS animations (navigation pushes, alert presentation) well
  enough, or the runner needs a short settle after a push.

## Next permitted action

The owner reviews the iOS routes in [design.md](design.md#ios), especially the alert press through the private
`handler` key, before the changes are committed.
