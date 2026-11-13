# MAUI runners: first figures (2026-11-13)

## Windows PC

One round each, after a warm-up, `--no-build`, `perf.ps1` (scratchpad). Wall seconds per folder: the whole
`dotnet test`, so it includes the host's start (about 2 s, see Headless) and the app's launch. Android: emulator
`emulator-5554`, animations off; Appium 3.1.2 with uiautomator2 6.7.10 on port 4724 (4723 held by another
server); the fixture reinstalls the app at each start, as by default.

Android Appium set the size: in its 5 minutes it reached five of the planned eight folders, so every combination ran
those five. Every run was green (one Text test skips on Android, one Scroll test on Headless).

| Folder (tests) | Android Appium | Android DevFlow | Windows FlaUI | Windows DevFlow | Headless |
| --- | --- | --- | --- | --- | --- |
| Buttons (5) | 32.3 | 11.8 | 17.6 | 21.6 | 1.9 |
| Toggle (13) | 65.7 | 20.0 | 10.1 | 17.8 | 2.2 |
| Text (21) | 108.4 | 27.8 | 16.8 | 27.3 | 2.9 |
| Selection (6) | 31.6 | 13.0 | 5.7 | 12.2 | 2.0 |
| Scroll (7) | 78.7 | 14.0 | 6.3 | 12.6 | 2.1 |
| **Total (52)** | **316.9** | **86.8** | **56.7** | **91.7** | **11.2** |

## Reading

- On Android, DevFlow is about 3.7 times faster than Appium on the same tests: the start is cheaper (12 s against 32 s
  for Buttons) and each test is several times quicker (Text 19 s against 98 s of test time).
- On Windows, FlaUI beats DevFlow by about 1.6 times. DevFlow's launch costs about 8–10 s per folder more
  (Selection: 12.2 against 5.7 wall, the same 4 s against 2 s of test time), and its tests are slower too (Text 19 s
  against 13 s).
- DevFlow costs about the same on Android as on Windows (86.8 s against 91.7 s): the agent's HTTP round trips set the
  pace, not the platform.
- Headless is 5 times faster than the quickest real head, almost all of it the host's start.
- FlaUI's Buttons includes a first-launch cost (17.6 s against about 6 s for the other folders), even after the
  warm-up.

## Next, if wanted

- Find where DevFlow's launch time goes on Windows (process start, agent ready, first tree).
- Run DateTimes, Dialogs and Collection on the four quick combinations, which fit easily.

## MacBook Pro: iOS (2026-11-13)

A different machine, so its own table: Apple M1 Pro, 16 GB, macOS 27.2, Xcode 27.2; simulator iPhone 17 Pro on
iOS 26.0, booted once and left running. One round each after a warm-up, `--no-build`, `perf.sh` (session scratchpad).
Appium 3.9.0 with xcuitest 12.19.1 on port 4723, the plain app build; DevFlow with the `-p:BruinetDevFlow=true` build.
The 5-minute cap was not applied: iOS Appium ran all five folders, so the columns stay the same.

| Folder (tests) | iOS Appium | iOS DevFlow | Headless |
| --- | --- | --- | --- |
| Buttons (5) | 57.2 | 18.7 | 1.7 |
| Toggle (13) | 184.1 | 31.1 | 2.0 |
| Text (21) | 328.2 | 45.4 | 2.4 |
| Selection (6) | 77.2 (4 failed) | 48.5 | 1.8 |
| Scroll (7) | 84.5 | 33.2 | 1.8 |
| **Total (52)** | **731.2** | **176.9** | **9.7** |

DevFlow and Headless were green (one Text test skips on iOS, one Scroll test on Headless). On iOS Appium four Picker
tests failed, not retried: `Picker_Reset_ClearsSelection`, `Picker_SelectByIndex_UpdatesStatus`,
`Picker_MultipleSelections_UpdatesStatus` and `Picker_GetSelectedText_ReturnsCorrectValue` each lost the
"Option 1" `PickerWheel` element (stale element) after about 10 s.

| Measure | iOS Appium | iOS DevFlow |
| --- | --- | --- |
| Test time of the set (trx) | 657 s | 76 s |
| Time per test, Text | about 15 s | about 1.6 s |
| Slowest test | `SearchBar_Clear_TextIsRemoved`, 25.8 s | `SearchBar_ResetAll_TextIsCleared`, 3.0 s |
| Launch to first test, per folder | 11-16 s | 11-14 s; once 39 s (Selection) |

### Reading

- On iOS, DevFlow is about 4 times faster than Appium on the same tests, and its tests alone about 9 times faster.
  The launch costs both about the same; the gap is in each action.
- iOS Appium is the slowest combination measured: Text alone takes 5.5 minutes, so the Android 5-minute cap would have
  stopped it during Text.
- iOS DevFlow costs about twice Android DevFlow (176.9 s against 86.8 s), on another machine. The tests run at about
  1.5 s each against Android's 1 s, and the launch (`simctl` and the agent) takes about 12 s a folder.
- DevFlow's Selection is the launch: 39 s before its first test against 11-14 s for the others, its tests no slower.
  Once, so far.
- Headless takes 9.7 s here against 11.2 s on the Windows PC: the two machines are close, so the tables can be read
  side by side roughly, not cell by cell.

### Next, if wanted

- Find why the Picker wheel goes stale under Appium on iOS 26 (the wheel is replaced while the picker opens).
- Run DevFlow's Selection again to see if the 39-second launch repeats.
