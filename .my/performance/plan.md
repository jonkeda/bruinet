# MAUI runners: a small performance run

Comparable figures for each head and platform from very small runs: the same eight folders everywhere, at most
5 minutes a run on Android, a few minutes elsewhere. Not a full suite run.

## Combinations

| Head / platform | Runner, test project | Notes |
| --- | --- | --- |
| Windows | FlaUI (default), `tests/Bruinet.Maui.UITests` | Plain sample app build. |
| Windows | DevFlow, `tests/Bruinet.Maui.UITests` | `-p:BruinetDevFlow=true`, `bin/DevFlow/` build. |
| Headless | MauiHeadless (`BRUINET_HEAD=headless`), `tests/Bruinet.Maui.UITests` | `@device` tests skip; count them apart. |
| Android emulator | Appium uiautomator2, `tests/Bruinet.Maui.UITests.Mobile` | Appium 3.1.2 with uiautomator2 6.7.10 is installed; start `appium` first. |
| Android emulator | DevFlow, `tests/Bruinet.Maui.UITests.Mobile` | `bin/DevFlowAndroid/` apk, `emulator-5554`. |

On the Mac (a second machine, so its figures get their own table; see [Machines](#machines)):

| Head / platform | Runner, test project | Notes |
| --- | --- | --- |
| iOS simulator | Appium XCUITest, `tests/Bruinet.Maui.UITests.Mobile` | Appium 3.9.0 with xcuitest 12.19.1; start `appium` first. Plain build in `bin/PlainIos/`. |
| iOS simulator | DevFlow, `tests/Bruinet.Maui.UITests.Mobile` | `-p:BruinetDevFlow=true` build in `bin/Debug/net10.0-ios/iossimulator-arm64/`. |
| Headless | MauiHeadless, `tests/Bruinet.Maui.UITests` | The same head as on Windows, run again here: it ties the two tables together. |

Out: the Shell app (one extra Windows DevFlow run of its 15 tests if time allows). Windows through Appium's windows
driver is not a MAUI runner and stays out. Android on the Mac stays out: it was measured on the Windows machine.

## Machines

Figures from different machines are not compared cell by cell. Each machine has its own table; the Headless column,
run on both, shows how far apart the machines are.

| Machine | Combinations |
| --- | --- |
| Windows PC | Windows FlaUI, Windows DevFlow, Headless, Android Appium, Android DevFlow. |
| MacBook Pro, Apple M1 Pro, 16 GB, macOS 27.2, Xcode 27.2 | iOS Appium, iOS DevFlow, Headless. Simulator: iPhone 17 Pro, iOS 26.0. |

## The set

The same folders on every combination, run in this order, one `dotnet test` per folder:
`Buttons`, `Toggle`, `Text`, `Selection`, `Scroll` (52 tests).

Android Appium, the slowest, sets the size: in its 5 minutes it reached these five of the eight planned
(`DateTimes`, `Dialogs` and `Collection` were cut). Figures: `results.md`.

## How to run

- Build every app and test project first; the timed run is `--no-build`.
- One warm-up run of `Buttons` per combination, not counted (first launch, JIT, emulator caches).
- Then the set once: quick figures, not exact ones.
- Android: emulator cold-booted once with `-no-snapshot-load`, headless, nothing else running on it.
  Stop the set after 5 minutes of wall time and record which folder it reached.
- iOS: simulator booted once and left running; nothing else installed on it runs. The Appium and DevFlow builds share
  a bundle id, so the warm-up of each combination absorbs the reinstall. WebDriverAgent is built in Appium's warm-up.
- Run with `batch.ps1` (scratchpad) or a copy of it in `tools/Scripts`: it caps each folder, kills only its own
  PID tree and only apps under the build path. Never `taskkill /IM testhost.exe`. On the Mac, `perf.sh`
  (session scratchpad) does the same with `timeout` per folder.
- Machine quiet: no studio, no other test runs, no VS Code test explorer runs during the timed set.
- `--logger trx` per folder for per-test durations.

## What to record

One table per machine in `.my/performance/results.md`, a column per combination:

| Measure | From |
| --- | --- |
| Wall time per folder | `batch.ps1` start/stop |
| Passed, failed, skipped per folder | `dotnet test` summary |
| Time per test (slowest 3) | trx durations |
| App launch to first lookup | fixture's first test minus the rest's mean |

Then one comparison table: folder by combination, wall seconds. Failures are listed, not retried; a known failure
(row selection, the timing-dependent carousel test) is noted beside its folder.

## Open

- Appium needs its own port when another server holds 4723 (`-AppiumPort`, 4724 here).
- Whether to add a per-action figure (tap, fill, read) from DevFlow's agent log; only if the folder figures show a
  gap worth explaining.
