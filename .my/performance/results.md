# MAUI runners: first figures (2026-11-13)

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
