# Bruinet review: multiple heads

## Verdict

"Same scenario → service → headless MAUI → real device" is three claims of very different strength sold as one. Across the UI heads of one app (MauiHeadless, Windows, Android, iOS; bUnit and the browser), it is a sound architecture that has been built carefully. It is not new: Google shipped the same idea for Android (Espresso tests on Robolectric or on a device), and Flutter and Avalonia have it too. It is new to MAUI, which had nothing like it. The service head is mostly vocabulary discipline plus a convenience: in your own flagship Todo sample, 2 of 41 scenarios run there, and the one scenario shared with a UI head never touches the UI. This is a real change in test economics, which matters a lot for agent loops, but not in test strategy. Its main risk is not abstraction leaks, which you document unusually well. It is that a green light from a cheap head looks exactly like a green light from the judge, and nothing in the default reporting makes the difference hard to miss.

---

## 1. First, separate the three claims

The question frames "the execution layer as a deployment target for the test." Bruinet's own docs show three different relationships hiding under that phrase:

| Transition | What actually changes | How "same scenario" holds up |
| --- | --- | --- |
| MauiHeadless ↔ Windows ↔ Android ↔ iOS (and bUnit ↔ browser) | The **driver** under the same app, page objects and phrases | Strong. Only the fixture changes (AD-030, AD-032), and the evidence is real (325 tests in ~20 s headless) |
| MAUI ↔ Web (Todo) | A **different app** that shares a vocabulary, with page objects and phrase classes of its own (AD-014) | Shared *specification*, two *implementations*. A parity tool, not a deployment target |
| UI heads ↔ `service` | **No screen at all**: page steps, control verbs and page phrases are a bind error (UATB013) | Weak. A scenario runs on both only if it was written without UI words in the first place |

The Todo sample's tags say it plainly. Of 41 scenarios in `Bruinet.Samples.Todo.Uat/Features`, 31 are `@maui @web`, 8 are `@maui` only, 1 is `@service` only and 1 is `@service @maui`. That shared one, TOD.09.3, is "When I restart the app / Then the database should have todos": its assertion is below the UI on both heads. So in practice, "the same scenario from service to device" means "a scenario that never mentions the screen can also run where there is no screen." That is true, but it is far narrower than the pitch.

**Recommendation:** pitch it as *one scenario, many heads of one app; a service head for the scenarios whose assertion is below the UI*. The current wording invites a principal engineer to look up the numbers above and then discount the rest of the README.

## 2. Is it compelling, a convenience, or a shift?

- **Compelling architecture: yes, for the driver heads.** Keeping drivers behind `IMauiDriver` / `IHtmlDriver` costs little, and AD-032 records the payoff: a third driver cost "two projects and no change to a page object." Ending calls fast with `DeviceOnlyException` / `BrowserOnlyException` (via `IEndsCall`) is a good detail, because a headless run never burns its wait budget on something it cannot answer. The binding-time UATB013 is the right place to fail.
- **Maintenance convenience: yes, for the service head and the web/MAUI parity.** It stops a journey from being written twice under two names (AD-020's stated reason), and it makes the coverage report honest about tiers. That is valuable hygiene, not architecture.
- **A shift in test strategy: no. A shift in test economics: yes.** The strategy is unchanged: broad journey tests, judged by a real device at the gate. What changes is that broad tests become cheap enough to run on every edit, on Linux and macOS, inside an agent's loop. That matters more in 2026 than it would have in 2018, because a coding agent that can run 325 UI-level tests in 20 s without an emulator is a different kind of worker. Make that argument, not the pyramid one.

## 3. Arguing against it as hard as I can

### 3.1 It does blur the pyramid, just in a new way

The classic pyramid ranks tests by **scope** (how much of the system a failure could be in) and **cost**. Headless heads collapse the cost axis and leave scope where it was. A journey scenario run headless is still a broad test: when it fails it can point at the page, the view model, the service, the fake backend or the head itself, and it covers combinations badly. Before, cost pushed rules down into unit tests. Remove the cost and that pressure goes with it. The outcome is an "ice-cream cone that runs fast": hundreds of Given/When/Then journeys checking things a unit test would pin more precisely.

Your own evidence points this way. AD-030 cites a finding that "about half of its UI scenarios check a rule's value or a page's state, which bUnit can show without a browser." The other reading is that half of those scenarios should not have been UI scenarios at all. Bruinet's answer moves them to a cheaper head. A sceptical test architect's answer moves them to a unit test, and the coverage report would no longer count them as UI.

To be fair, the tag-as-tier rule (a scenario's head tags are its tiers in the coverage report) is more honest than most BDD setups. It still classifies by *where the test ran*, not by *what it proves*. A headless run of a layout-sensitive scenario is counted the same as one that proves logic.

### 3.2 What each head silently stops proving

| Head | Stops proving, without saying so | How a team would notice today |
| --- | --- | --- |
| `service` | Bindings, view-model to view wiring, validation shown in the UI, input formatting, navigation, accessibility. Also the composition: `TodoHost` is a headless host, not the app's startup | Only if the same scenario is also tagged for a UI head, and almost none are |
| MauiHeadless | Layout, occlusion and clipping; virtualization; platform handlers and native controls; `#if` platform code; packaging, assets and startup; lifecycle (backgrounding, rotation, the keyboard covering a field). **Concurrency**: the dispatcher is a queue drained at every call, under a virtual clock. The determinism is the selling point, and it is also exactly what hides races, reentrancy and ordering bugs | `@device` skips are listed. A scenario that *passes for the wrong reason* (a button hidden by layout counts as visible) is not; it is caught only when the Windows gate happens to look at the same thing |
| MauiHeadless ("answers as Windows does") | Android's and iOS's trees: Android omits what is not drawn, and headless shows every row | Only on an Android run, which is nightly or picked by the touch map |
| Windows (FlaUI), *the gate* | **Real input.** AD-005 says MAUI on Windows uses no physical input at all, and AD-008 routes gestures through a bridge the app publishes. So the merge gate proves the automation tree and rendering, not that a person's tap, swipe or keystroke works. Also nothing about Android or iOS | Not at all, unless someone knows AD-005. Your README's "keep the real device as the judge" reads as if it covered input |
| Android (Appium) | iOS, plus whatever the schedule skips | The schedule |
| iOS | Little in practice: the differences table says "not tried" in several iOS cells and "from how XCUITest reports its tree" | Nobody, until a release |
| bUnit | CSS, JS interop, render modes and prerendering, the SignalR circuit, reconnects | `@browser` skips; again, false passes are invisible |

The common failure is that **a pass is a pass**. A scenario green on headless and green on Windows shows the same tick. The README says "the device stays the judge at a gate," but that is a policy in the documentation, not a property of the report. A team falls back to "it's green headless, merge it" the first week the Windows runner is broken.

### 3.3 Where the abstraction leaks

Your docs already list the leaks, which is to your credit. The list is also the strongest case against the idea, because it is long and it grows with every app:

- **Timing.** The app runs only when a call lets it. A wait outside the driver must pass `whileWaiting: IsIdle`, a fixture needs `WaitForQuiet`, and `TimeFollowsWallClock` was added so timers behave "as on a device." Each of these is a place where a test written headless-first hangs or passes differently on a device.
- **Navigation.** A page navigated away from "answers for about a second" on Windows, "leaves at once" on Android, and leaves "after the push animation" on iOS. A page under a modal is in the tree on Windows and was "not tried" on Android.
- **Visibility.** A row partly on screen is visible in one head, not visible in another, and left out in a third. "Visible" headless means "on the shown page with no hidden ancestor": a different predicate under the same name.
- **Text.** `SemanticProperties.Description` beats the button text on Windows and iOS but not on Android or headless. The documented fix is "read both, or assert what the app shows," which is a scenario adapting to the heads.
- **Platform-only controls.** Picker popups and MediaElement transport controls cannot be reached headless.
- **Gestures.** These go to verb sinks in the app (`GestureAutomation.Sink`) on headless and through the UIA bridge on Windows. Neither is a gesture.
- **The app adapting to the driver.** "The app turned its sticky headers off on Windows to get past it: a concession to the driver." This is the leak to fear most: the *product* changes so the abstraction holds.
- **MAUI internals.** MauiHeadless reaches taps, focus, bindings and pending dialogs "by reflection in one place." Every MAUI minor release is a possible silent break of a head that claims fidelity.

The Kofflard evidence in AD-032 is the most important data point in the repository, and it cuts both ways. Ten differences were found; one was the app, one was MAUI, and **eight were Bruinet's own device drivers**. So the largest risk in a multi-head system is not that headless lies about the app. It is that each head is a separate product with its own bugs, and the gate's verdict depends on the driver as much as on the app. One app, on one day, is also a thin base for the "which head, when" policy.

### 3.4 Does it push scenarios to the lowest common denominator?

Yes, by design. AD-014 rules out "head-specific wording for the same step." That is right for specs and wrong for platform UX. Swipe-to-delete on Android and a context menu on Windows are not "the same step" to the user, but under AD-014 they must be worded as one ("I delete X"), and the platform behaviour moves into page objects, where nobody reading the scenario sees it. The escape hatches (`@windows`, `@device`, `@browser`, `Platform is ... or Headless` branches in page objects) each wear down the claim a little. Already 8 of 41 Todo scenarios are MAUI-only, and 30 `@windows` tags appear across the sample.

The deeper version is that scenarios drift toward **intent words** ("I sync" rather than "I tap Sync") because those are the only words that run everywhere. Intent-level scenarios are good specifications and poor UI tests: they cannot say *how* the user did it, so they cannot catch a broken *how*.

## 4. Genuinely novel versus common

**Common, with clear prior art:**
- One acceptance spec, several protocol drivers. Dave Farley's four-layer acceptance tests (spec, DSL, protocol driver, system); the Screenplay pattern (Serenity BDD and Serenity/JS, where one task runs through `BrowseTheWeb` or `CallAnApi`); Behat suites running the same features against domain and UI contexts ("Modelling by Example", around 2014).
- Same UI test, in-process or on a device. Google's AndroidX Test "write once, run anywhere" (Project Nitrogen, I/O 2018): Espresso on Robolectric or a device, with Compose tests the same. Flutter's `WidgetTester` in `flutter test` versus `integration_test` on a device. Avalonia.Headless.
- Same test, several targets of one layer. Playwright projects (browsers and devices), Appium and Xamarin.UITest (Android and iOS), the dotnet/maui repository's own UITests over Appium (Windows, Android, iOS, Mac Catalyst), Uno.UITest.
- Tags choosing where a scenario runs: Cucumber, Reqnroll and SpecFlow tag expressions.

**Genuinely distinctive, as far as I know:**
- **MauiHeadless itself.** A handler-less MAUI test host with real bindings, Shell, `NavigationPage`, dialogs, a virtual clock and Essentials, usable without Bruinet. The MAUI ecosystem has nothing comparable, and Microsoft has not built one.
- **A head as a runtime switch on one project** (`BRUINET_HEAD`), with device-only members failing at once and skips that give a reason. Most precedents use separate source sets (Android's `test` and `androidTest`) or separate projects.
- **The service head bound against the UI catalog** so that a UI step is a *bind error*, not "no binding", plus coverage that counts one scenario for two tiers.
- **The written discipline:** a promotion policy per head, a "differences between the heads" table where every row is fixed or pinned by a test, and "broken when" invariants. Discipline like this is rare in open source and is the most mature part of the idea.
- **One scenario driving several apps on different heads at once** (`mixed`: Windows, web and Android). Unusual. I know of no mainstream .NET tool that does it out of the box.

The combination is novel. No single part of it is.

## 5. Competitors on this specific axis

| Tool | Relevant position | Against Bruinet's heads |
| --- | --- | --- |
| Playwright | Real browsers only; component testing has stayed experimental | Bruinet's bUnit head is something Playwright deliberately does not offer. Playwright's team would argue that is the point |
| Appium | One test, Android and iOS | Bruinet sits on top of it; it does not compete |
| Selenium | One layer | Not comparable |
| FlaUI | One layer, Windows | Bruinet's Windows driver |
| Reqnroll / SpecFlow | Tags and multiple bindings can approximate heads; no driver abstraction | Bruinet's head model is more opinionated and better enforced (UATB013, coverage by tag) |
| bUnit | In-process Blazor, its own API | Bruinet wraps it so page objects run unchanged |
| MAUI UITesting (dotnet/maui) | Appium on every platform; no in-process head | MauiHeadless is the clear gap Bruinet fills |
| Cypress | Real browser; component testing in the browser | Different bet |
| TestComplete, Ranorex | One recorded test across desktop, web and mobile engines | Similar "engine" idea, no in-process head, no Markdown specs |
| Tricentis Tosca | Model-based modules over engines; the closest commercial cousin of "a test is independent of its engine" | Tosca wins on enterprise reach, Bruinet on code-level fidelity and speed |
| Xray, TestRail | Management, not execution | Would consume Bruinet's per-head results if exported, which is a reason to export them |
| AI-driven tools (Testim, mabl, Applitools Autonomous, Copilot-style agents) | Mostly real browsers or devices, self-healing locators | Fast deterministic heads are what agent loops need. This is where the idea lands best |

## 6. How leaders would likely react

- **Microsoft (.NET/MAUI):** most interested in MauiHeadless, and most uneasy about its reflection into MAUI internals, which they would not promise to keep stable. A likely answer is "propose the hooks upstream." The Playwright team (also Microsoft) would be politely sceptical of any head that is not a real browser.
- **Google:** "We did this. It took years, and fidelity was the whole battle." They would ask for your false-pass rate between heads, not your speedup. Their habit of classifying tests by *size* (small, medium, large) rather than by spec would make them push back on counting one scenario for several tiers.
- **JetBrains:** tooling-first. They would like the LSP and MCP surface, question a Markdown dialect of Gherkin that Rider's Gherkin support does not understand, and see heads as a run-configuration question.
- **SmartBear (Cucumber, TestComplete, BitBar):** "BDD across layers is what Cucumber has preached since 2010; teams fail on collaboration, not runners." Little threat to them, except in a .NET desktop niche against TestComplete.
- **Tricentis:** would recognise their own "engine-independent test case" idea, and ask where the risk-based coverage, the no-code authoring and the enterprise reporting are.
- **GitHub:** the most receptive. Fast, deterministic UI heads that run on Linux runners, driven over MCP, are exactly what Copilot's coding agent needs to check UI work without a device farm. They would care about Actions minutes and flakiness, and headless helps with both.

## 7. Risks, ranked

1. **The judge gets skipped.** The policy ("a gate never passes on headless alone") lives in documentation. Under deadline pressure it erodes, and the report does not show it happening.
2. **Driver fidelity is several products.** Eight of ten found faults were Bruinet's drivers. Every head added is another conformance target to maintain, and a small team carries all of them.
3. **MAUI version churn** breaks MauiHeadless's reflection silently or loudly with each release.
4. **Concurrency bugs pass headless by construction** (a deterministic dispatcher drained at every call).
5. **Scenarios at the lowest common denominator** stop specifying platform UX, and page objects quietly gain branches by platform.
6. **The iOS claim is ahead of its evidence.**
7. **The merge gate does not test real input** on Windows, which readers of "real device as the judge" will assume it does.

## 8. Recommendations

1. **Make proof level a first-class result, not a doc rule.** For each scenario, report a head-by-result matrix and "last judged on a device: commit or date." Fail a release gate when a device-tagged scenario has not been judged within N commits. Export it to Xray or TestRail.
2. **Turn the differences table into a driver conformance kit.** One suite of probes (visibility, partial rows, modal cover, navigation leave, text vs description, paging) that every head must pass, run on each MAUI or driver upgrade. Your own data says this is where the bugs are.
3. **Run differential checks.** On a device run, record each step's tree and compare it with the headless tree for the same step. Flag "visible headless, hidden on device" automatically, instead of waiting for a test to happen to assert it.
4. **Add a chaos dispatcher mode to MauiHeadless**: reordered, delayed or interleaved queue work, with seeded randomness. Keep determinism as the default and make races findable.
5. **Push logic down, not just sideways.** Lint scenarios whose steps never touch a control and suggest `service` or a unit test. Do not let "it's cheap now" stand in for "it's the right scope."
6. **Watch the drift toward the lowest common denominator**: the share of single-head scenarios, the number of platform branches in page objects, and the count of `@device` tags. Allow platform-specific scenarios openly (`@android` swipe-to-delete) rather than hiding platform UX in page objects.
7. **Be precise in the README:** "the Windows gate uses UI Automation and an in-app gesture bridge, not physical input"; "iOS: tree mapping done, runs not yet routine"; "the service head runs scenarios whose assertions are below the UI."
8. **Upstream or isolate MauiHeadless's internals.** Pin a MAUI canary job, and propose the missing public hooks (tap, focus, pending dialogs) to dotnet/maui. It is the most valuable part, and the one most likely to be broken from outside.
9. **Lead with the agent-loop story.** "UI-level checks in seconds, on Linux, deterministic, with the device as a scheduled judge" is the argument that wins in 2026. "Execution layer as a deployment target" invites the prior-art comparison you lose.
