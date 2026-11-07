# Plan: faster Android and iOS UI test runs

Date: 5 November 2026. Purpose: one ordered list of the Bruinet changes that still cut Appium run time, on Android (UiAutomator2) and iOS (XCUITest) together. This plan replaces the open steps of [the Android plan](../android-performance/plan.md) and [the iOS plan](../ios/perf-plan.md); their measurements stay where they are.

Bruinet changes go to `move`, and the submodule bump goes to Construction `Dade_55_Closer`. Probes and scripts under `.my/` are not committed.

## Current state

Already on `move`, with the numbers in the source plans:

| Change | Head | Where it is described |
|---|---|---|
| Install only when the APK changed (`AndroidInstalledBuild`) | Android | Android plan, P1 |
| Readiness read once per attempt (`AttemptMemo`) | both | Android plan, P2 |
| Immutable facts by element id, row ids once per attempt, no visibility read before a read, alive check folded into bounds | D1 and D2 both, D3 and D4 Android | [design-fewer-reads.md](../android-performance/design-fewer-reads.md) |
| Search swipes with `mobile: scrollGesture` instead of `UiScrollable` | Android | `AppiumMauiDriver.FindByScrolling` |
| `waitForIdleTimeout` 0, quiescence off | iOS | iOS plan, step 5 |
| Recovery forgets cached page roots, which removes the 2.2 s stale reads | both | iOS plan, steps 6 and 7 |
| `Rect` is one `GET rect`, `TagName` read once | both | iOS plan, results |
| `AppiumPlatform` with `IosPlatform` and `AndroidPlatform` (part 1) | both | Bruinet `989c9726` |

Where the time goes now:

| Cost | Android | iOS |
|---|---|---|
| Session start, per fixture class | 5.3 s (no reinstall) | about 22 s |
| One request | 8–40 ms | 70–160 ms |
| Requests | lane test 1,205, of which about 615 `rect` and 185 `enabled` | short set about 6,000 |
| One swipe | about 1 s | 1–3 s (cool-off 2 s) |

On both heads the remaining cost is the number of requests and the session starts. iOS pays about five times more per request, so every request removed counts five times more there.

## Measures

| Name | Head | What | Baseline |
|---|---|---|---|
| Android pilot | Android | Kofflard `LaneDiagnosticTests`, one test, `measure.sh` and the Appium log | 44–45 s, 1,205 requests |
| Android button | Android | Bruinet `ButtonTests.Button_MultipleTaps_IncrementsCount` | 11.0 s |
| iOS short set | iOS | Construction MaterialsOverview, GroupedListGallery, ContactListSmoke, GarbageOverview (30 tests) | 856 s test time, about 5,971 requests |
| Android short set | Android | the same four Construction classes on the emulator | not yet measured (step 0) |

For each step, record: wall time, requests by command (from the Appium log), and the number of requests over 1 s. Each step names the reading that proves it.

## Steps

Ordered by gain per effort. Each step changes one thing and is measured on its pilot before the next starts.

### 0. Baselines on this Mac

- Android on macOS has no runner yet: `run-uitests-android.ps1` needs Windows, and `pwsh` and `adb` are not on the PATH here. Add `.my/performance/run-android.sh` that mirrors `scripts/run-uitests-ios.sh` (emulator `Brinell_Android`, Appium on 4723, `BRUINET_HEAD`=Android), and put the SDK's `platform-tools` on the PATH.
- Run the Android pilot and the Android short set once. Run the iOS short set once, to confirm 856 s still holds on the current `move`.
- Stop if the Android short set does not pass on the current code. Its failures are a separate fix list, not part of this plan.

### 1. No enabled check on a read (Android P3)

- `GetText`, `GetAttribute` and `IsVisible` do not ask for `enabled`. Only actions do.
- Where: `ViewBase` reads, `ControlCalls.GetWithElement`.
- Proof: `enabled` count on the Android pilot (185 now) and in the iOS short set. Expected 3–4 s on the Android pilot; more on iOS per request removed.
- Pin: a unit test in `ListAndPageRootPinTests` that a read does not ask `IsEnabled`, and an action still does.

### 2. Shown when found on iOS (D3 for iOS)

- Check with the iOS probe whether XCUITest's `elements` returns only elements with `visible=true` when `snapshotMaxDepth` is default. If it does, set `FindsOnlyVisibleElements` for iOS too. If not, leave D3 Android-only and record why.
- Proof: `displayed` count in the iOS short set.
- Done (6 November): not adopted. XCUITest also finds elements with `visible=false`, so D3 stays Android-only. See the Results row.

### 3. Fewer requests per row check (iOS H3, Android "a name read is 12 requests")

- Look up Busy once per attempt, not per row (`AttemptMemo`).
- Do not read `name` when `text` already answered.
- Extend `ElementFact` caching to iOS for the element type and AutomationId. First check with the probe that WDA keeps the same element id for a recycled cell; if a recycled cell keeps its id but changes its identifier, cache only the type.
- Proof: requests per row check, from about 8 (iOS) and 12 (Android) to 4 or fewer.

### 3b. Animations off in the iOS test build

- Construction, `Dade_55_Closer`: when `CONSTRUCTION_DISABLE_ANIMATIONS` is true, `MauiProgram` sets `UIView.AnimationsEnabled = false` and the key window's `Layer.Speed = 100`, under `#if IOS`. The Bruinet iOS fixture sets the variable, as it does `CONSTRUCTION_USE_MOCK_BACKEND`. Android already has `disableWindowAnimation`.
- This does not stop a list coasting after a swipe; that is scroll physics.
- Measure first: the iOS short set with the flag off and on, the cool-off left at 2 s. Expected about 8 % (published), roughly 60–70 s; adopt it only if the short set is faster and green.
- Then, separately: lower `animationCoolOffTimeout` (for example 0.5 s) and make Bruinet wait until the list's position settles after a scroll, instead of relying on the fixed cool-off. Measure on the short set's list classes; keep it only if no row is read while the list is still moving.

### 4. Session cost

1. Split the 22 s iOS session start: WDA launch, app install, app launch. Use the Appium log timestamps.
2. If install is part of it, add the iOS counterpart of `AndroidInstalledBuild`: compare the `.app` hash with a record in the simulator's data folder (`xcrun simctl get_app_container`) and skip the install when both match. `BRUINET_ALWAYS_REINSTALL=1` still installs.
3. If WDA launch is the bulk, try `usePrebuiltWDA` together with `derivedDataPath`, and `webDriverAgentUrl` against a WDA that stays running between classes.
- Proof: `POST session` time per class on the iOS short set. Expected 22 s to under 10 s per class.

### 5. Reuse the session across classes (Android P8, iOS report)

- Decided (5 November): the Construction app gets a **Clear data** button on the Developer page. It clears what a fresh install would not have: the cache database, the user database, preferences and the stored mock authentication.
- One session per xUnit collection of classes that test one app. Between classes the fixture taps Clear data, then `terminateApp` and `activateApp`. Both heads use the button, so the reset path is the same; iOS has no data clear without a reinstall.
- The button is a Construction change on `Dade_55_Closer`, with a translation in every `ConstructionTerms` variant and an AutomationId. Build it before the Bruinet fixture change.
- Proof: one `POST session` for the short set on each head, and every test still passes.
- Expected: 5 s per class after the first on Android, 10–22 s on iOS.
- Done (6 November): not adopted for speed. All Construction classes already share one fixture in one process; the four sessions came from the runners, which start one process per class. With one process and the clear between classes, both heads are slower than one process per class, because a clear (navigation, sign-out, relaunch, sync) costs more than a new session. The runners keep one process per class. Kept: the button, the clear between classes when a process runs several classes, and a fixed `ResetAppState`. See the Results rows.

### 6. Fewer page checks per attempt

- About 600 of the Android pilot's requests are the page root's `rect`, one per attempt by rule (R2, R6). The only cut that keeps the rule is a longer pause between attempts on Appium heads.
- This lowers the request count, but probably not the wall time. `Poller.Until` pauses after each attempt, so a call that succeeds on its first attempt does not change, and a call that waits for the app notices the change up to one interval later. Fewer attempts only save wall time if the reads themselves slow the app down. That can happen on iOS, where WDA's accessibility snapshot competes with the app's main thread.
- So this step is only a measurement: the iOS short set at 100 and 250 ms. Adopt it only if wall time drops; otherwise drop the step.
- Done (6 November): dropped. 250 ms gives the same request count as 100 ms and a test time within noise. See the Results row.

### 7. Attributes with the search result (both heads)

- UiAutomator2 and XCUITest both support the settings `shouldUseCompactResponses=false` and `elementResponseAttributes` (for example `name,text,rect,enabled`). A search then returns those facts with each element in one request.
- The Appium C# client keeps only the element id, so the probe first checks that a raw `ExecuteCustomDriverCommand` or an HTTP call gets the attributes back.
- If it works, use those facts within the attempt that found the element (`AttemptMemo`), never across an action.
- Proof: requests per row check and on the list pilots. This is the largest remaining cut, and it touches Bruinet's read rules, so it needs a short design note before it is built.
- Done (6 November): adopted for iOS only. The design note is [step7-design.md](step7-design.md). An action now ends what the attempt knew, and each wait inside an attempt reads afresh. Android keeps its plain finds, because a read there costs only about 12 ms. See the Results row.

### 8. Find a row on the device (Android P5, iOS counterpart)

- Android: `UiScrollable(...).scrollIntoView(new UiSelector().text("…"))` for a text predicate, as `FindItemByText`. Its earlier cost came from flinging to the top first; offer it only for a known keyed row, and measure it against the current swipe search.
- iOS: `mobile: scroll` with `predicateString` and `toVisible`, on the list element.
- Fall back to the current search on other heads and when the predicate is not a text match.
- Proof: card search time on the Android pilot (5–9 s now) and list classes in the iOS short set.
- Done (6 November), iOS: the slow part was not the search but the gesture. XCUITest's `mobile: scroll` took 7.5 s a page; scroll-to-find now uses the list scan's swipe, about 1.7 s. The predicate scroll was not tried, because it runs the same slow XCUITest scroll. Android: not built. The short set never scrolls to find on Android, and list searches take a predicate Bruinet cannot send to the device, so a text search needs a new keyed API and a test that uses it. Left open.

### 9. Finish the `AppiumPlatform` move (iOS step 10, part 2)

- Move the branches that are still left: back navigation, popups and alerts, the keyboard, scroll into view, pickers, the flyout check. One group at a time, no change in behaviour.
- After each group: Bruinet unit tests, headless, one iOS class and one Android class.
- This makes steps 2, 3, 7 and 8 per-head changes instead of `if` branches. Do it before step 7 if steps 2 and 3 add more platform branches.
- Done (6 November): every branch moved, in two groups (navigation, keyboard and dialogs; then taps, scrolling, pickers and the flyout). The only `MauiPlatform` check left in the driver is the constructor's guard. Bruinet tests, headless, iOS `ContactListSmokeTests` (11 of 11) and Android `GroupedListGalleryTests` (5 of 5) passed. Bruinet `96da4d1d`.

### 10. Verification runs

- Both short sets, then the full class runs: `.my/uitesting/run-classes.sh` for iOS and its Android counterpart. Compare with `summary-pass6.tsv`.
- Only after the short sets are clearly faster and green.

- Done (6 November). iOS short set: 30 passed, test time 646 s, wall 711 s (baseline 851 s test time). Android short set: 30 passed, test time 386 s (baseline 371 s, within run-to-run spread; no step aimed at Android timing). Full iOS runs of the eight pass-6 classes: 43 passed, 2 failed, 20 minutes against 40 in pass 6. The two failures, `ListsGalleryTests.ScrollToLastRow_LoadsNextPage` and the `JournalPdfJourneyTests` setup, failed the same way in pass 6.

## Decisions needed

- Step 5: decided, a Clear data button on the Developer page.
- Step 6: no decision needed unless the measurement shows a wall-time gain.
- Later, outside this plan: two simulators or emulators in parallel.

## Open questions

- Android `waitForIdleTimeout`: 0 crashed UiAutomator2 once, so it is 100 now. Retest 0 on Appium 3.9 with one class.
- Android swipe of about 1 s: how much is the emulator's software GPU? Measure on a snapshot boot with hardware acceleration.
- iOS `snapshotMaxDepth` and `useNativeCachingStrategy` were not tried. Try them in the probe during step 2.
- `ListsGalleryTests.ScrollToLastRow_LoadsNextPage` is flaky on iOS, before and after the changes. Do not use it as a measure.

## Rules

- One change at a time, measured on its pilot. No full run before step 10.
- No longer timeouts as a fix.
- Nothing kept across an action: a memo lives for one attempt, and only immutable facts live longer.
- No page-source snapshot reads without an AD.
- Do not reduce motion through the simulator setting: it changes product behaviour.
- Do not run while a test build is in progress.
- Keep the numbers for each step in this file under a Results section.

## Results

iOS short set: four classes, 30 tests, iPhone 17 simulator. Times are seconds.

| Step | Test time | Requests (time) | Note |
|---|---|---|---|
| Baseline | 851 | 5,974 (999) | `enabled` 378, `POST session` 4 × about 42 (first one installs cold for 86) |
| 1. Liveness after a read | 840 | 5,892 (916) | `enabled` 296. Committed `eb496c97` on `move`. |
| 3a. AutomationId kept for the attempt | 800 | 5,517 (861) | `attribute/name` 977 to 605. A row scan read each row's id twice. Committed `c579c47f`. |
| 3b. Animations off (`CONSTRUCTION_DISABLE_ANIMATIONS=1`) | 783 | 5,559 (840) | `execute/sync` 108.6 to 94.2. Gesture median 1.6 s. Construction `ea703315`, on by default in `run-uitests-ios.sh`. |
| 3b. Cool-off 0.5 s (with animations off) | 782 | 5,551 (845) | No gain; reverted. The cool-off is not the limit once animations are off. |
| 3c. Tab parts kept for the attempt; busy label kept with the page root | 743 | 5,245 (808) | Child finds 1,128 to 807. Headless set passes. Committed `d3d83a0a`. |
| 4. No reinstall of an unchanged `.app` | 759 | 5,266 (822) | Later sessions start in 12-14 s instead of about 21 s; one took 35 s waiting for the app to be idle at launch. Committed `f59c67d3`. |
| 4b. No idle wait when WDA launches the app (`waitForIdleTimeout` capability 0) | 770 | 5,577 (801) | Session start 84 s to 55 s over four classes; the slowest went from 35 s to 20 s. Total within noise. Committed `596f9530`. |
| Android baseline (emulator 4 GB, 16 GB disk) | 421 | 5,825 (323) | 17 passed, 13 failed. The same failures happen on Bruinet before step 1, so they are not caused by this plan. 12 are Android gaps in Bruinet: `List` controls and the search box's text field. |
| Android fixes (Bruinet `9444f52d`, Construction `948468cf`) | 371 | 4,422 (379) | 30 passed. Fixed: list and list-item lookups, AutomationIds found in tree order, no swipe on a list that cannot scroll or next to the gesture bar (it sent the app home), each new APK installed (runs had tested an old one), and offline mode on the device. This is the Android baseline from now on. |
| 2. Shown when found on iOS | — | — | Not adopted; nothing to commit. A side survey of a live GroupedListGallery session found `visible=false` elements in finds: empty labels with zero width, and a label inside the window that the bottom of the screen covers (`Diagnostic & Crash Tests` at y 822). Treating found as shown would call that label shown and tap whatever covers it. The 668 `displayed` reads (79 s) stay. A later option: a predicate find with `visible == 1`, which is a find-rule change and needs its own design note. |
| 5. One process, no clear between classes (iOS) | 765 | 5,306 (763) | One `POST session` of 22 s instead of four totalling 55 s. Not isolated: classes see each other's data. |
| 5. One process, Clear data between classes | iOS 825, Android 388 | iOS 5,724 (808), Android 4,543 (374) | 30 passed on each head. Slower than one process per class (770 and 371): each clear costs about 20 s on iOS. Not adopted for the runners. While building it, `ResetAppState` turned out to do nothing on iOS (no `bundleId` capability) and to lose mock mode on Android (`activateApp`, then a positional intent that dropped the extras). Fixed in Bruinet `3fcf2a97`; button and per-class clear in Construction `89caa153`. |
| 6. Pause between attempts 100 ms (control) and 250 ms | 778 and 771 | 5,513 (827) and 5,502 (789) | 30 passed in both. Almost no call needs a second attempt, so the pause hardly changes the request count; the 7 s gap is noise. Dropped; the pause stays 100 ms. |
| 7. Facts with the find (iOS) | 682 | 4,167 (706) | 30 passed (wall 752 s); one Android class passed too. Reads fell: `attribute/name` 650 to 130, `text` 550 to 135, `displayed` 666 to 315. The remaining `rect` and `displayed` reads are probably of elements kept from an earlier attempt, such as page roots; not yet checked. Bruinet `b2e1936b`. |
| 8. iOS scroll-to-find swipes instead of `mobile: scroll` | 650 | 4,233 (672) | 30 passed. Scripts 95 s to 73 s. Bruinet `007db318`. |
| 9. Platform branches moved into `AppiumPlatform` | - | - | No change in behaviour; one class per platform passed. Timed in step 10. Bruinet `96da4d1d`. |
| 10. Verification | iOS 646, Android 386 | iOS 4,257 (666), Android 4,391 (395) | 30 passed on each head; iOS wall 711 s. Full iOS class runs: 20 minutes against 40 in pass 6, same two known failures. |
| 11. Kept page root confirmed by finding it again (iOS) | 639 | 4,023 (667) | 30 passed. `rect` 595 to 347 and `displayed` 315 to 67 (55 s less), but 259 more root finds took 48 s more: a root find searches the whole app, about 185 ms against about 190 ms for the two reads. Net 7 s, inside the run-to-run spread. Not adopted; reverted. |
| 12. Free-point tap uses the find's bounds (iOS) | 642 | 4,225 (659) | 30 passed. `rect` reads unchanged at 628: most taps find no nested controls and return before reading bounds, so the tap was not a source. Not adopted; reverted. |

Session start, measured in step 1: about 5 s reinstall, about 15 s WDA session startup (which includes the app launch), about 3 s other. Skipping the reinstall saves about 20 s on the short set, less than the plan expected.

Later candidates seen in the logs:

- Each test re-checks that Welcome, Login and the sync choice are absent (`ConstructionAuthenticationManager.EnsureAuthenticated`), three finds or about 0.4 s per test.
- The remaining iOS reads come from kept page roots (`IsCachedRootValid`, about 180 per short set) and list items (`ItemObjectBase.IsUsable`, about 50): the find returns facts only in the attempt that ran it. Finding the root again costs as much as reading it (step 11).
- Startup polls the sync page and the dashboard in a loop with only a `SpinWait` (`PostLoginSyncAutomation.WaitForDashboard`): 280 sync-page finds in the short set. Most of that is real waiting for the app.
