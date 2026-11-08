# Report: why the iOS UI tests are slow, and what we did about it

Date: 5 November 2026. Scope: the Construction UI tests on the iPhone 17 simulator (iOS 27.0), driven by Bruinet through Appium 3.9 and the XCUITest driver.

## In short

The iOS run is now about 1.5 times faster than it was, and every test in the measured set passes. The biggest cause was a single WebDriverAgent setting, not Bruinet's own code. What remains is the normal cost of driving iOS through Appium: about 0.1 s per request, and thousands of requests per class.

Headless runs the same tests about 20 times faster than iOS. iOS stays necessary only for the bugs that headless cannot see.

## How we measured

We used a short set of four classes from different modules: MaterialsOverview, GroupedListGallery, ContactListSmoke and GarbageOverview, 30 tests in total. Each change was measured on this set, one change at a time, and every run passed all 30 tests.

To check whether Bruinet was at fault, we also wrote a small Appium script without Bruinet. It drove the same navigation through raw HTTP calls. Its taps took the same 19–20 s as Bruinet's, so the stall sat below Bruinet.

## What we found

**The long pauses came from WebDriverAgent's idle wait.** Before each tap, XCTest waits for the app to be idle. Bruinet set that wait to 1 second, expecting a short limit. In practice, any value above 0 still runs the check, and on a page that is animating it took 15–20 s per tap. Only 0 skips it. The Appium documentation confirms that 0 turns the check off completely.

**Reading an element that no longer exists costs about 2.2 s on iOS.** XCTest tries to find the element again before it reports it gone. A fresh search that finds nothing takes 0.2 s. Bruinet's navigation recovery checked pages that earlier tests had kept in memory, which caused about 66 of these slow reads per short-set run. Recovery now forgets those pages first, and the count dropped to 5.

**Bruinet asked for some facts twice.** Every position read was two requests, because the Appium C# client asks for the position and the size separately. The element type was also read again on every text read.

**One setting looked good but broke lists.** Turning off WDA's animation cool-off made drags faster, but then rows were read while a list was still scrolling. We kept the default.

## Results

| Change | Short set, test seconds |
|---|---|
| Before (estimated from earlier class minutes) | about 1,260 |
| Idle wait set to 0 | 1,058 |
| Stale reads avoided | 941 |
| Element type read once | 906 |
| Position read in one request | 856 |
| Same four classes headless | 43 |

After these changes, almost no request takes longer than a second. The exceptions are scroll scripts, which wait for the scrolling to settle, and the start of each session, which takes about 22 s per class. The rest is about 6,000 requests at 70–160 ms each. Appium itself adds only 25–30 ms of that; the remainder is WebDriverAgent reading the accessibility tree of a busy MAUI page.

## What other teams see

Our experience matches what is published. It is not a problem peculiar to this app.

- Appium's own [WebDriverAgent slowness guide](https://appium.github.io/appium-xcuitest-driver/latest/troubleshooting/wda-slowness/) names the idle check and animations as the main cause of slow actions. It adds that an app that keeps its main thread busy is "hardly automatable" without changing the app.
- The XCUITest driver's [settings reference](https://appium.github.io/appium-xcuitest-driver/12.15/reference/settings/) documents a default idle wait of 10 s, and says 0 disables the check. It also documents the 2 s animation cool-off after each action.
- A [series on XCUITest speed](https://swiftly-developed.com/blog/animations-off-and-parallel-simulators-for-xcuitest/) measured idle waits of up to 60 s on animating screens while the app stayed responsive. Turning animations off inside the app saved them about 8% on their heaviest class. They also warn not to emulate Reduce Motion, because it changes product behaviour.
- A [Stack Overflow question](https://stackoverflow.com/questions/41277026/disabling-waiting-for-idle-state-in-ui-testing-of-ios-apps) about an endlessly animating button describes the same never-idle stall in native apps.
- Typical published request times on iOS are 30–100 ms for an id or predicate lookup, and 1–5 s for an XPath lookup ([DeviceLab](https://devicelab.dev/blog/appium-slow-7-fixes-that-work)). Our 70–160 ms per request is in that range for large MAUI pages.
- The .NET MAUI team itself tried to speed up its Appium suite by opening each test page directly instead of navigating through a gallery. They [reverted that change](https://github.com/dotnet/maui/pull/39621) within a day because it caused more failures on Android and iOS. That shows how fragile speed-ups at the navigation level are.

We found no published numbers for Appium with .NET MAUI on iOS specifically. Microsoft's [MAUI UI testing guide](https://learn.microsoft.com/en-us/dotnet/maui/deployment/ui-testing?view=net-maui-10.0) explains the setup but says nothing about speed.

## What this means

The tests use iOS the way a user does, through accessibility, and each step is a round trip to the simulator. A realistic floor is a few minutes per class, not seconds. Further gains of the same size would need one of these:

- **Fewer requests per step.** Each row check still makes about eight requests. WebDriverAgent can return attributes together with a search result, which could halve the reads on list pages. This change touches Bruinet's read rules and needs care.
- **Fewer session starts.** About 22 s per class goes to starting the session. Running several classes in one session would save that, but it raises the risk that one class leaves state behind for the next.
- **Parallel simulators.** Two simulators would roughly halve the wall time, but need two Appium ports and twice the memory.
- **Animations off in the app's test build.** This is the approach the XCUITest series measured; it gave them about 8%.

## Recommendation

Run headless on every change, because it is 20 times faster and catches most failures. Run iOS for the classes that cover iOS-only behaviour (row buttons, recycled cells, alerts, popups), and run the full iOS set nightly rather than on every change. The list of what headless misses is in [headless versus iOS](../../../.my/uitests/headless-vs-ios.md).

## Open items

- `ListsGalleryTests.ScrollToLastRow_LoadsNextPage` is flaky on iOS with the old and the new settings alike.
- About half of Bruinet's platform branches have moved into `AppiumPlatform`. Back navigation, popups, the keyboard, scrolling and pickers still branch.
- The cause analysis is in [the RCA](../../../.my/rca/2026-11-06-ios-test-run-idle-waits.md), and the step-by-step measurements are in [the plan](perf-plan.md).
