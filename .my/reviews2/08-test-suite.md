# Bruinet's test suite: review and proposals

Written 2026-11-11 at `b2ea4570`. Review 8 of `additional-reviews-proposal.md`.
Read from the 49 project folders under `tests/`; tests are counted as test attributes in a project's own files (`[Fact]`, `[Theory]`, the twelve attributes derived from them, and NUnit's and MSTest's; a theory counts once).
No UI test was run. Stryker ran on `Bruinet.Core` and `Bruinet.Bdd` in a separate worktree; nothing else was built.

## Outcome

The suite holds 3,217 tests in 45 of the 49 projects; the other four only support them.
The unit tier is where the weight is, and the control contracts are the right idea: one abstract class per Core interface, bound per head, with a head's differences written as skips.
The idea is half used. Eight of the sixteen contracts are bound by no head, WPF, WinForms, Stride and native Android bind none, and the call model that lives in `Bruinet.Core` is proved once per stack in copied files while Core's own tests never run it.

Three smaller things stand out. Six unit projects with 158 tests are in no script and no CI step.
Fifteen tests carry a literal skip, three of them hiding a real difference between stacks.
Five tests only print what they found and cannot fail on it, and five more pass unless a call throws.

## The projects

Tier names used below: *unit* runs in the test process on fakes; *head in process* runs real controls on bUnit or MauiHeadless; *UI* starts a browser, a window, a game or a device.

### Unit (26 projects, 2,179 tests)

| Project | Tests | What it proves |
| --- | --- | --- |
| `Bruinet.Appium.Tests` | 13 | Appium's errors map to Bruinet's; Android host and recorder discovery. |
| `Bruinet.Bdd.Tests` | 171 | The feature parser, binder, values, config, runner channel and breakpoints; 2 benchmarks opt in. |
| `Bruinet.Bdd.Workspace.Tests` | 54 | Workspace, suites, app sources, and the preview against stored snapshots. |
| `Bruinet.Blazor.Tests` | 177 | Each Blazor control forwards to a Moq element. |
| `Bruinet.Cli.Tests` | 352 | The generator, the language server, the MCP tools, data checks and test selection; one class scrapes in Chromium. |
| `Bruinet.Contracts.Tests` | 67 | DTO, exchange and round-trip contract checks. |
| `Bruinet.Core.Tests` | 93 | Settings, artifacts, logging, the app set, and the architecture rules of AD-033. |
| `Bruinet.FlaUI.Tests` | 5 | FlaUI's errors map to Bruinet's. |
| `Bruinet.Html.Tests` | 73 | Html's call model, waits, scopes and collections on a Moq driver. |
| `Bruinet.Logging.Tests` | 2 | An app's `ILogger` lines land in the running step's log. |
| `Bruinet.Maui.Tests` | 252 | Maui's call model, readiness, containers, Appium locators and iOS routes on Moq. |
| `Bruinet.Mocking.Data.Tests` | 46 | The SQL log, replay and schema snapshot on SQLite; 8 need a SQL Server. |
| `Bruinet.Mocking.Data.Hook.Tests` | 3 | The test-id hook of the SQL replay. |
| `Bruinet.Mocking.Data.Net48.Tests` | 3 | The SQL log's netstandard build on .NET Framework; 2 need a SQL Server. |
| `Bruinet.Mocking.Http.Tests` | 21 | HTTP record and replay over WireMock. |
| `Bruinet.Mocking.Tests` | 54 | The mock API server, sensors, and the mock-body analyzer. |
| `Bruinet.Narrator.Tests` | 177 | Speech, SSML, video scripts, the clip library and secrets files; 3 need ffmpeg. |
| `Bruinet.NativeAndroid.Tests` | 55 | Native Android's call model, locators and options on Moq. |
| `Bruinet.Recording.Tests` | 25 | The recorders; 2 need ffmpeg, 1 needs Chromium. |
| `Bruinet.Scraper.Tests` | 222 | Scraping, exploring, drafting pages and tests; seven classes drive Chromium on the test site. |
| `Bruinet.Stride.Tests` | 6 | Stride's call model on a fake game. |
| `Bruinet.Stride.Automation.Tests` | 11 | The in-game automation server's options and UI handler. |
| `Bruinet.TestData.Tests` | 13 | The test data validator. |
| `Bruinet.Uat.Tests` | 224 | The UAT engine: catalog, subjects, arguments, heads, the runner process. |
| `Bruinet.WinForms.Tests` | 34 | WinForms' call model, controls and configuration on a fake UIA app. |
| `Bruinet.Wpf.Tests` | 26 | WPF's call model, controls and configuration on a fake UIA app. |

### Head in process (5 projects, 206 tests)

| Project | Tests | What it proves |
| --- | --- | --- |
| `Bruinet.Html.Bunit.Tests` | 123 | Html's control members and context on bUnit; binds the control contracts (11 skip overrides counted here). |
| `Bruinet.Maui.Headless.Tests` | 15 | The sample pages on MauiHeadless, through the device suite's page objects. |
| `MauiHeadless.Tests` | 64 | MauiHeadless itself (dispatcher, tree, dialogs, chrome) and its examples. |
| `MauiHeadless.Tests.NUnit` | 2 | MauiHeadless needs no xUnit. |
| `MauiHeadless.Tests.MSTest` | 2 | The same on MSTest. |

### UI (12 projects, 709 tests)

| Project | Tests | What it proves |
| --- | --- | --- |
| `Bruinet.Html.UITests` | 26 | Html controls on the Blazor sample in Chromium, 2 feature files; binds the control contracts in Chromium. |
| `Bruinet.Blazor.UITests` | 34 | Blazor controls on the same sample in Chromium, 2 feature files. |
| `Bruinet.Maui.UITests` | 373 | Every Maui control on the two sample apps: FlaUI on Windows, MauiHeadless in CI; 4 feature files; binds seven contracts. |
| `Bruinet.NativeAndroid.UITests` | 29 | The native gallery app on a device, 2 feature files. |
| `Bruinet.WinForms.UITests` | 50 | The WinForms login form through FlaUI, 1 feature file. |
| `Bruinet.Wpf.UITests` | 15 | The WPF sample through FlaUI, 2 feature files. |
| `Bruinet.Stride.UITests` | 30 | The sample game over its pipe, 1 feature file. |
| `Bruinet.Uia.Tests` | 44 | The UIA bridge between two processes on a real window; 20 of them pin constants and are unit tests. |
| `Bruinet.Bdd.Workspace.UITests` | 11 | The Scenario Preview page in Chromium during a paused run. |
| `Bruinet.Bdd.VisualStudio.UITests` | 4 | Debugging a scenario in Visual Studio; opt in. |
| `Bruinet.Presenter.Bdd.Tests` | 87 | Presenter's services as unit tests, and one theory that drives the Presenter app over 9 feature files. |
| `Bruinet.Sql.Spike.Tests` | 6 | The SQL replay spike: the SQL sample on both Html heads; needs a database. |

### Support (6 projects, 123 test attributes that run only where bound)

| Project | Tests | What it is |
| --- | --- | --- |
| `Bruinet.ControlContracts` | 111 | Sixteen abstract contracts, one per Core control interface. |
| `Bruinet.ControlContracts.Html` | 12 | The contracts bound to Html's controls for any Html head; 9 Blazor control facts, 3 skip overrides. |
| `Bruinet.ControlContracts.Html.Site` | 0 | Serves the contract pages to Chromium. |
| `Bruinet.Scraper.TestSite` | 0 | The site the Scraper and Cli tests scrape. |
| `Bruinet.Uia.TestHost` | 0 | The window that hosts the UIA bridge for `Bruinet.Uia.Tests`. |
| `Bruinet.Maui.UITests.Mobile` | 0 | `Bruinet.Maui.UITests`' sources compiled for the Appium head. |

## Findings

### 1. Half the control contracts are bound by nothing

- Sixteen contracts hold 111 facts. Eight are derived by a head: Element, Clickable, Focusable, Text, EditableText, Toggle, Range and Selector.
- The other eight are derived nowhere, so their 42 facts never run: `DateContract` and `TimeContract` (`tests/Bruinet.ControlContracts/DateTimeContracts.cs:7`, `:46`), `ExpandableContract` and `TabContract` (`ExpandableAndTabContracts.cs:7`, `:59`), `ScrollableContract`, `SwipeableContract` and `RefreshableContract` (`GestureContracts.cs:8`, `:77`, `:126`) and `ProgressContract` (`ProgressContract.cs:7`).
- Only three heads bind anything: bUnit (`tests/Bruinet.Html.Bunit.Tests/Contracts/BunitContracts.cs:21`), Chromium (`tests/Bruinet.Html.UITests/Tests/Contracts/ChromiumContracts.cs:10`) and Maui (`tests/Bruinet.Maui.UITests/Tests/Contracts/MauiContracts.cs:15`).
- Maui binds seven classes and leaves out Element, Clickable and Text; its comment says the contracts have run on MauiHeadless only (`MauiContracts.cs:8`).
- WPF, WinForms, Stride and native Android bind none, although the skip reason in `HtmlBindings.cs:75` already describes how WPF and WinForms answer.

### 2. The call model lives in Core and is proved once per stack, in copies

- `tests/Bruinet.Wpf.Tests/CallModelTests.cs` and `tests/Bruinet.WinForms.Tests/CallModelTests.cs` hold the same six tests (`:13` to `:77` in both); with the stack's name replaced, `diff` reports one hunk.
- `ControlTests.cs` in the same two projects holds the same ten tests (`:11` to `:157`); the diff is two hunks, a slider against a progress bar at `:123`.
- Native Android repeats the first three under other names (`tests/Bruinet.NativeAndroid.Tests/CallModelTests.cs:16`, `:31`, `:43`), and Stride has its own six (`tests/Bruinet.Stride.Tests/StrideCallModelTests.cs:14`).
- `PredicateWaitTests` exists in Html, Maui and native Android (`tests/Bruinet.Html.Tests/PredicateWaitTests.cs`, `tests/Bruinet.Maui.Tests/Semantic/PredicateWaitTests.cs`, `tests/Bruinet.NativeAndroid.Tests/PredicateWaitTests.cs`); `ScopeReadinessTests` and `CollectionTests` exist in Html and native Android.
- Core's own tests do not run that code at all: in the mutation run below, `Calls/ControlCalls.cs`, `Calls/ControlCall.cs`, `Calls/FirstOutcome.cs`, `Calls/ObservationLog.cs`, `Collections/CollectionObjectBase.cs` and `Scopes/ContainerObjectBase.cs` had no mutant killed and none covered.

### 3. Control tests that a contract or a unit test would prove

- `Bruinet.Maui.UITests` has 42 tests named `…_IsExists_ReturnsTrue`, `…_IsVisible_ReturnsTrue` or `…_IsEnabled_ReturnsTrue` in 19 files, for example `tests/Bruinet.Maui.UITests/Tests/Range/SliderTests.cs:31`, `:44` and `:57`. That is `ElementContract`, which Maui does not bind.
- `Bruinet.Blazor.Tests` has 35 tests named `IsExists_…`, `IsVisible_…` or `IsEnabled_…` in 18 files, each reading back what its Moq element was set up to say (`tests/Bruinet.Blazor.Tests/Controls/ButtonControlTests.cs:26`, `:40`, `:54`). The contracts already run those members on real markup on two heads.
- `Bruinet.Html.UITests` and `Bruinet.Blazor.UITests` click the same counter on the same sample page (`tests/Bruinet.Html.UITests/Tests/Controls/ButtonControlTests.cs:10`, `tests/Bruinet.Blazor.UITests/Tests/Controls/ButtonClickTests.cs:9`), and both re-check visible and enabled, which `ChromiumElementContract` proves.
- `Bruinet.WinForms.UITests` tests one login form from four classes. The role theory is written three times with the same body (`Tests/ContainerControlTests.cs:132`, `Tests/LoginPageTests.cs:107`, `Tests/InputControlTests.cs:146`), and clearing the form four times (`ContainerControlTests.cs:66`, `LoginPageTests.cs:83`, `InputControlTests.cs:112`, `AdvancedLoginTests.cs:63`).
- `tests/Bruinet.Maui.UITests/TestTimingReportTests.cs:13` and `:32` are marked `Category=Unit` and use no app, yet build and run with the UI suite.
- `tests/Bruinet.Uia.Tests/ContractTests.cs:21` holds 20 tests on constants in a project that otherwise needs a desktop.

### 4. Six unit projects run nowhere

CI's test step is `tools/Scripts/coverage.ps1 -Headless -Browser` (`.github/workflows/ci.yml:90`). Its list (`coverage.ps1:48` to `:73`) names 26 projects and its comment leaves out two on purpose.

| Project | Tests | Named in a script |
| --- | --- | --- |
| `Bruinet.Contracts.Tests` | 67 | No |
| `MauiHeadless.Tests` | 64 | No |
| `Bruinet.Mocking.Http.Tests` | 21 | No |
| `Bruinet.Logging.Tests` | 2 | No |
| `MauiHeadless.Tests.NUnit` | 2 | No |
| `MauiHeadless.Tests.MSTest` | 2 | No |

`test-maui-headless.ps1:37` runs `Bruinet.Maui.UITests` and the Todo sample, not MauiHeadless's own tests.
`Bruinet.Bdd.Workspace.UITests` needs only Chromium, as the `-Browser` suites do, and is not in that list either (`coverage.ps1:86`).

### 5. Skips

Fifteen tests carry a literal `Skip`, in three files and four reasons; the proposal's "six" was not found by any count.

| Where | Tests | Reason, and whether it holds |
| --- | --- | --- |
| `Bruinet.Html.Bunit.Tests/Contracts/BunitContracts.cs:27`, `:30` | 2 | bUnit has no pointer. Holds; Chromium runs them. |
| `BunitContracts.cs:38` to `:50`, `:72` to `:81` | 9 | bUnit has no script. Holds; Chromium runs them. |
| `Bruinet.ControlContracts.Html/HtmlBindings.cs:78`, `:81`, `:84` | 3 | A missing element answers differently per stack. An open defect, not a head's limit. |
| `Bruinet.Maui.Tests/FluentChainingTests.cs:66` | 1 | "RightClick uses Selenium Actions … integration test only". Stale: the UI suite's only right-click test expects `NotSupportedException` (`Bruinet.Maui.UITests/Tests/Background/FocusVerbTests.cs:193`). |

- The three in `HtmlBindings.cs` skip on both Html heads, so the shown and hidden halves of `IsVisible` and `IsEnabled` go unproven by the contract too. The reason points to `.my/coverage/plan.md`, which no longer exists.
- A further 74 tests skip by environment through ten attributes, each with a reason that names the switch: `DeviceFact` 27, `WindowsOnlyFact` 20, `SqlServerFact` 10, `FfmpegFact` 5, `IdeFact` 4, `BrowserFact` 3, `BenchmarkFact` 2, `ChromiumFact`, `ProbeFact` and `StressFact` 1 each. These are in order.

### 6. Tests that cannot fail

A script read every test body for an assert, a wait, a throw check or an `Assert…` control call; 54 had none, most of them one-line delegations that do assert further down. Five are reports, not tests:

- `tests/Bruinet.Uia.Tests/BridgeVisibilityTests.cs:139` catches every exception and ends in `Assert.True(true)` (`:162`).
- `tests/Bruinet.Maui.UITests/Tests/Range/StepperTests.cs:57` prints what `Invoke` did and catches what it throws (`:74`).
- `tests/Bruinet.Maui.UITests/Tests/Navigation/NavigationProbeTests.cs:97` prints a table; its comment says it does not fail on a negative.
- `tests/Bruinet.Maui.UITests/Tests/Diagnostics/BridgeTreeDumpTests.cs:69` prints the tree after a swipe.
- `tests/Bruinet.Html.Bunit.Tests/SpikeTests.cs:43` prints what bUnit does with a held node.

These pass unless the call throws, which is a weak proof of the name they carry:

- `tests/Bruinet.WinForms.UITests/Tests/LoginPageTests.cs:40` types a password and checks nothing.
- `tests/Bruinet.WinForms.UITests/Tests/InputControlTests.cs:42` is named for the password box and asserts the username.
- `tests/Bruinet.WinForms.UITests/Tests/ContainerControlTests.cs:32` sets seven controls and reads none back.
- `tests/Bruinet.NativeAndroid.Tests/CallModelTests.cs:198` says the password is confirmed by length and never checks what was typed or logged.
- `tests/Bruinet.Core.Tests/PhysicalInputTests.cs:61` and `tests/Bruinet.Html.Bunit.Tests/MemberTests.cs:565` are honest "does not throw" tests and can stay.

### 7. Spikes and leftovers

- `Bruinet.Sql.Spike.Tests` is a spike by its own description (`tests/Bruinet.Sql.Spike.Tests/Bruinet.Sql.Spike.Tests.csproj:5`), in the solution, with 10 committed recordings; its plan `.my/sql-replay/plan.md` is gone. `coverage.ps1:23` leaves it out, so it is built by every CI run and run by none.
- `tests/Bruinet.Html.Bunit.Tests/SpikeTests.cs:10` answers "the four questions of the bUnit head's spike (AD-030)"; the decision is made.
- Seven project files and two sources cite `.my/coverage/plan.md` or `.my/sql-replay/plan.md`, neither of which exists (for example `tests/Bruinet.ControlContracts/Bruinet.ControlContracts.csproj:4`, `tests/Bruinet.Mocking.Data.Hook.Tests/Bruinet.Mocking.Data.Hook.Tests.csproj:5`).
- `Bruinet.Presenter.Bdd.Tests` is named as a unit project, references `Bruinet.Maui.UITests` and drives the Presenter app; `coverage.ps1:22` leaves it out for that reason.

## Mutation sample

`tools/Scripts/mutate.ps1 -All -Library <name>` in a worktree at `b2ea4570`, Stryker 4.16.0 from the tool manifest, on a machine busy with other builds.

| Library | Result | Reading |
| --- | --- | --- |
| `Bruinet.Core` | 33.97% in 228 s: 494 killed, 19 timed out, 306 survived, 691 not covered | Of the mutants a test reaches, 63% die. Half of what goes undetected is code no Core test runs. |
| `Bruinet.Bdd` | 46.15% in 676 s: 1,723 killed, 80 timed out, 805 survived, 1,299 not covered | Of the mutants a test reaches, 69% die. The parser and values are well held; the binder and the runner are not. |

- In Core, 40 of the 75 files with mutants had none killed; they hold 498 of the 997 undetected mutants.
- The largest holes are the call model of finding 2: `Calls/ControlCalls.cs` 100 uncovered, `Calls/ObservationLog.cs` 61, `Collections/CollectionObjectBase.cs` 58, `Scopes/ContainerObjectBase.cs` 30.
- Where Core's tests do reach, the weak spots are `Artifacts/DefaultTestArtifactPathProvider.cs` (17 killed, 45 survived) and `Composition/TestComposition.cs` (45 killed, 38 survived).
- In Bdd, `BddBinder.cs` is the weakest large file: 88 killed against 70 survived and 207 not covered. `BddMarkdownParser.cs` (378 killed, 290 undetected) and `BddValues.cs` (344 killed, 148 undetected) carry most of the score.
- Three runner files had nothing covered from `Bruinet.Bdd.Tests`: `Runner/BddRunnerClient.cs` (103 mutants), `Runner/BddWarmRunners.cs` (62) and `Runner/BddShadowCopy.cs` (55). Their tests sit one project up, in `tests/Bruinet.Uat.Tests/BddRunnerProcessTests.cs` and `BddWarmRunnersTests.cs`, so `mutate.ps1` scores them as untested.
- `BddDiagnosticsFormatter.cs` (65 mutants) had nothing covered either; it writes the messages a failed bind shows.

## Proposals

1. **Bind the eight unused contracts or delete them.** Date, Time, Expandable, Tab, Scrollable, Swipeable, Refreshable and Progress on Maui (MauiHeadless first), Date, Time and Progress on Html; a contract no head binds after that goes.
2. **Bind Element, Clickable and Text on Maui, then delete the 42 `Is…_ReturnsTrue` tests** from `Bruinet.Maui.UITests`. That moves them from one per control on a device to one binding per control on MauiHeadless.
3. **Bind the contracts on the WPF and WinForms fake apps** (`FakeApp.cs` in both unit projects), and on Stride's fake game and native Android's. Then `ControlTests.cs` in WPF and WinForms joins into the bindings and one of the two copies goes.
4. **Move the call model's tests down to `Bruinet.Core.Tests`.** One `CallModelTests` on a fake element in Core covers find-again, the caller's timeout, one log pair, wait-for-enabled, app gone and page not shown. The per-stack files keep only what the stack adds (native Android's scrolling and password cases, Stride's stale identity). `PredicateWaitTests`, `ScopeReadinessTests` and `CollectionTests` follow the same way.
5. **Resolve the missing-element difference** behind the three skips in `HtmlBindings.cs:78` to `:84`: either the docs change to what Html and Maui do, or the generated controls answer null. Until then, split each test so the shown and hidden halves run.
6. **Delete the 35 Moq `IsExists`, `IsVisible` and `IsEnabled` tests in `Bruinet.Blazor.Tests`** once `BlazorControlsBinding` covers every Blazor control; keep the Moq tests that prove an argument or a call count.
7. **Join the duplicates in the UI suites.** In `Bruinet.WinForms.UITests`, keep one role theory and one clear test and fold `ContainerControlTests` into `InputControlTests`. In `Bruinet.Html.UITests` and `Bruinet.Blazor.UITests`, drop the visible and enabled checks the Chromium contract proves, and keep one counter click between the two.
8. **Add the six unrun projects to `coverage.ps1`'s list**, and `Bruinet.Bdd.Workspace.UITests` under `-Browser`. A test in `Bruinet.Core.Tests` beside `SolutionTests` can then fail when a project under `tests/` is in no list and not named as left out.
9. **Move the tests that sit a tier too high.** `TestTimingReportTests` to a unit project; `Bruinet.Uia.Tests/ContractTests.cs` to a unit project that needs no desktop; Presenter's service tests apart from its one UAT theory, so the unit half can run in CI.
10. **Give the probes an opt-in attribute or delete them.** `StepperButtons_ReportInvokeBehavior`, `PageChrome_ReportsAddressability`, `DumpSwipeViewAfterOpening`, `OrdinarySearch_ReachesTheBridgeOrDoesNot` and `Bunit_NodeHeldAcrossRender_Reports` become `ProbeFact`s as `LookupCostProbeTests` is, so they stop counting as passing tests. Fix `PasswordBox_Enter_AcceptsPassword` and `LoginPage_CanEnterPassword` to assert on the password field, or delete them.
11. **Remove the leftovers.** Delete `Bruinet.Sql.Spike.Tests` with its recordings, or rename it and give it a run step if the replay still needs it. Delete `SpikeTests.cs` in `Bruinet.Html.Bunit.Tests` except `Find_ThenRerender_ElementReadsTheNewText` and `Render_SampleCounterPage_HasItsButton`. Delete the skipped `RightClick_ReturnsPageInstance`. Replace the dead `.my/…/plan.md` citations with the decision that came of them.
12. **Use mutation where it now points.** After proposal 4, run `mutate.ps1 -Library Bruinet.Core -Mutate '**/Calls/*.cs'` as the measure of whether the move worked. Add binder tests for the 70 survivors in `BddBinder.cs`, and a test of `BddDiagnosticsFormatter`.
13. **Put the runner's tests with the runner.** Move `BddRunnerProcessTests` and `BddWarmRunnersTests` from `Bruinet.Uat.Tests` to `Bruinet.Bdd.Tests` where they need no Uat type, or add `Bruinet.Uat.Tests` as a second test project for `Bruinet.Bdd` in `mutate.ps1`.

## Gaps

- No test in any project proves the Date, Time, Expandable, Tab, Scroll, Swipe, Refresh or Progress interfaces against their documentation.
- No shared test says what a missing element answers; three stacks answer three ways.
- `Bruinet.Core`'s call model, collections and containers have no test in Core.
- Nothing checks that every test project is run by something.
- `BddDiagnosticsFormatter` and most of `BddBinder` have no test that would notice a change.
- The Maui contracts have not run on Windows or Android.

## What is unknown

- Whether the six unrun projects pass today: they were not run for this review.
- How many of the 373 Maui UI tests overlap a contract beyond the 42 named: the rest were read by file name only.
- The mutation score of the stack libraries on their unit tests; only Core and Bdd were sampled.
- Whether the two runner test classes can move without a Uat type: their dependencies were not read.
