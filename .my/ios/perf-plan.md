# Plan: make the iOS UI test run faster

Purpose: cut the wall time of the Construction iOS UI tests on the iPhone 17 simulator (iOS 27.0) before the next full run. No full run until one test is clearly faster.

## Current state

- Pilot test: `ListsGalleryTests.ScrollToLastRow_LoadsNextPage`. It takes 3.5 min alone and fails within the class.
- Measured from the Appium log of the last single run: 210 s test time, 210 s inside 1,088 Appium HTTP calls. So the client is not sleeping; the time is spent waiting on Appium and WebDriverAgent (WDA).
- The calls that took longest:

| Call | Count | Total |
|---|---|---|
| `element/click` (3 taps at 15–20 s each) | 4 | 53 s |
| `mobile: dragFromToWithVelocity` (first drag 20 s) | 6 scripts | 26 s |
| `displayed`, `rect`, `element`, `elements`, `text`, `name` | about 1,000 | 120 s |

- These are the "long periods where nothing happens": one tap or drag blocks for 15–20 s. The navigation taps (More, Developer, Control gallery, Lists) and the first drag on the list each pay this.
- Smaller costs: a read on a stale element returns 404 only after about 2.1 s. `elements` for `ListRow` costs about 1 s each. Each row check makes about 8 calls (Busy lookup, `rect` twice, `displayed`, `text`, `name`, `attribute/name`), each 70 to 150 ms.
- Session settings sent by Bruinet (`MauiDriverFactory.SessionDefaults`): `waitForQuiescence=false`, `settings[waitForIdleTimeout]=1`. Not set: `snapshotTimeout` (WDA default 15 s), `animationCoolOffTimeout` (default 2 s), `reduceMotion`, `useNativeCachingStrategy`, and `snapshotMaxDepth`.

## Hypotheses

1. H1: a 15–20 s tap or drag is WDA's accessibility snapshot timing out (`snapshotTimeout`, 15 s, plus cool-off). The app keeps the tree busy, for example with an animating indicator or a deep MAUI tree, so WDA waits for the full timeout. This is the main suspect because the time matches 15 s plus about 2–5 s.
2. H2: WDA still waits for the app to go idle despite the settings, because a setting is ignored or named wrong on this Appium and XCUITest driver version.
3. H3: Bruinet is too chatty per row: duplicate `rect` reads, a Busy lookup on every attempt, and `name` read next to `text`. This costs a constant factor, not the long pauses.
4. H4: a stale element read costs 2 s because WDA retries the lookup before returning 404.

## Steps

1. Baseline the pilot. Run the pilot alone and keep `/tmp/appium.log` from an offset. Record the wall time, the time inside HTTP calls, and every call over 1 s. The numbers above serve as the baseline unless the code has changed.
2. Raw Appium probe, without Bruinet. Write `Brinell/.my/ios/probe/` as a small C# console project using `Appium.WebDriver`. It opens the same app with the same capabilities, taps More, Developer, Control gallery and Lists by accessibility id, drags the list up to row 40 and reads each row title. It logs a timestamp for every call.
   - If the raw taps also take 15–20 s, the cause is in WDA or the app (H1 or H2), not Bruinet.
   - If the raw run is fast, compare its call list with Bruinet's and find the extra waits (H3).
3. Test the WDA settings in the probe, one at a time, each through `POST /session/{id}/appium/settings`:
   - `snapshotTimeout`: 15, then 3, then 1.
   - `animationCoolOffTimeout`: 0.
   - `waitForIdleTimeout`: 0.
   - `customSnapshotTimeout`, `useFirstMatch`, `reduceMotion` capability `true`.
   Record the tap and drag times for each. Keep a setting only if the taps still land on the right control.
4. App-side check, only if H1 holds and no setting helps. Find what keeps the tree busy on those pages: an `ActivityIndicator` that is running while hidden, or a `Busy` label that updates. Use the probe's `/source` timing as the measure. Report it as an app finding; do not change the app in this plan.
5. Move the winning settings into Bruinet `MauiDriverFactory.SessionDefaults` for iOS, with a comment that gives the measured numbers. Run the Bruinet unit tests.
6. Reduce Bruinet chattiness where the log shows duplicates (H3). Read `rect` once per row check, look up Busy once per attempt instead of per row, and skip `name` when `text` already answered. Check each change in the pilot's call count.
7. Stale reads (H4): check whether WDA's stale 404 delay comes from `shouldUseCompactResponses` or element lookup retries. Change it only if the probe shows a setting that helps.
8. Rerun the pilot alone, then the full ListsGallery class. Then run a short set of five classes from different modules (Contacts, Garbage, Materials, Settings, Projects). Compare each class's time with `summary-pass6.tsv`.
9. Only when the pilot and the short set are clearly faster, at least 2 times, do a full run of every class with `run-classes.sh`.
10. Refactor, after the performance work: move the platform branches into one internal `AppiumPlatform` with `IosPlatform` and `AndroidPlatform`. The driver creates it once and the element uses it. Facts become properties (attribute names, locators, script names, session defaults) and behaviour becomes methods (free-point tap, popup dismissal, root check, scroll, locator mapping). Checks outside the Appium project (`CollectionObjectBase`, `ContentDialog`, `MauiDriverFactory`) ask the driver for a capability instead of the platform. Move one group at a time with no change in behaviour, and run the Bruinet unit tests and one iOS class after each group.

## Measures

- Wall time of the pilot, and of each class in the short set.
- Seconds inside HTTP calls, and the number of calls.
- The number of calls over 1 s, which should drop to almost none.

## Rules

- Change one setting or code path at a time, and measure after each change.
- Do not run a full run before step 9.
- Do not run while a test build is in progress; the build replaces the binaries.
- The probe stays under `.my/ios/probe/` and is not committed.
- Commit Bruinet changes to `move`, then bump the submodule in Construction `Dade_55_Closer`.

## Findings

### Raw probe (step 2)

The probe is `probe/probe.py`, plain HTTP with no client library. It needs the mock backend on port 9090; a temporary, uncommitted xUnit test hosted it with `MockBackendHostManager`.

With Bruinet's settings and `element/click`, one navigation tap still took 19-20 s, so the cause is WDA, not Bruinet. Which tap was slow changed between runs (the More tab once, Lists the next time).

| Mode or setting | Slowest tap | Whole flow |
|---|---|---|
| `element/click`, idle wait 1 (Bruinet today) | 19-20 s | 66-69 s |
| `mobile: tap` | 18 s | 66 s |
| W3C pointer actions | 1.4 s | 43 s |
| click, `snapshotTimeout` 3 | 1.8 s | 43 s |
| click, `waitForIdleTimeout` 0 | under 1 s | 44 s |
| click, `animationCoolOffTimeout` 0 | 1.7 s, drags 1.8 s instead of 3.2 s | 40 s |
| click, `reduceMotion` | 20 s | 63 s |

WDA does apply `waitForIdleTimeout` 1 from the capability, but at 1 the idle wait still runs and goes to its slow path. At 0 it is skipped.

### Settings in Bruinet (step 5)

`SessionDefaults` for iOS now sends `waitForIdleTimeout` 0. The animation cool-off stays at its default: at 0 the pilot read rows while the list was still coasting and the list element was replaced several times.

The pilot is not a usable measure: it also failed with the old value (12 rows materialized, page 2 not loaded), so it is flaky on its own. The short set is the measure instead.

### Stale reads and call count (steps 6 and 7)

A stale element costs XCTest about 2.2 s before WDA answers 404, against 0.2 s for a fresh lookup that finds nothing. About 66 stale reads per short set came from `NavigationRecovery` checking page roots cached by earlier tests. Recovery and `RegisterRootPage` now forget those roots first, which dropped stale reads to 5.

With no stalls left, the time is about 6,900 calls of 70-160 ms each. Appium's own overhead is 25-30 ms per call; the rest is WDA reading busy pages. Two cuts so far: the iOS `TagName` is read once per element, and `Rect` is one `GET rect` instead of two (the client reads `Location` and `Size` separately).

### Results on the short set

The short set is MaterialsOverview, GroupedListGallery, ContactListSmoke and GarbageOverview. All runs passed every test.

| Change | Test seconds |
|---|---|
| Before (pass 6, class minutes about 4, 5, 6, 6) | about 1,260 |
| Idle wait 0 | 1,058 |
| Plus stale-read fix | 941 |
| Plus `TagName` cache | 906 |
| Plus single `GET rect` | 856 |

At 856 s the short set runs about 1.47 times faster than before, short of the 2x the plan set for a full run, so step 9 was skipped. Of the remaining 5,971 calls, 85 take over 1 s: scroll and drag scripts (111 s, mostly WDA's animation cool-off) and session creation (88 s, about 22 s per class).

### Refactor (step 10)

`AppiumPlatform`, with `IosPlatform` and `AndroidPlatform`, now owns the locator mapping, the driver's chrome facts, the element's attribute facts and the gesture scripts (Bruinet `989c9726`). Behaviour is unchanged: the unit and headless tests pass, and five iOS classes passed after the change.

Still branching on the platform: back navigation, popups and alerts, the keyboard, scroll into view, the pickers, and the flyout check. These are larger methods that read the screen, so each needs its own move and an iOS run.

### Headless comparison

The same four classes on the MauiHeadless head take 43 s of test time (52 s wall, including process start), against 856 s on the iOS simulator: about 20 times faster. All 30 tests passed. Headless does not find the iOS-only bugs listed in `.my/uitests/headless-vs-ios.md`, so iOS runs stay needed for those.
