# Reviews2: task list

Agent document. The work the twelve reviews in this folder proposed, as tasks to do one by one and check off.
Branch `reviews` (worktree `E:\repos\Private\bru-reviews`), pushed to origin's `reviews` only; never to `move`, which another agent is changing. Merged into `move` later by the owner.

## Rules

- One task, one commit on `reviews`, its id in the subject (`R2-T07: …`). Check the box in the same commit.
- `Rnn Pn` = review nn, its proposal n. `Dn` = a design in `designs/`; a task that needs one waits for the owner's yes on it.
- Rebase often: before each task `git fetch origin`, and when origin's `move` has moved, rebase `reviews` on it first; again at the first conflict or build error that looks like drift. Force-push `reviews` with `--force-with-lease` after a rebase.
- Each task: build what it touches, run its unit tests; UI suites only where the row says. Smoke first, never the full UI suites (owner's rule).
- Runs that need the machine: **desk** = FlaUI on this Windows desktop (ask the owner for a quiet moment); **android** = emulator `Brinell_Android` and Appium 3.1.2 here; **mac** = iOS, no Mac available, so iOS stays read from source.
- A task that changes behaviour a consumer sees names the consumer: **tomorrow** (`E:\repos\Private\tomorrow`), **kofflard** (`E:\repos\Private\Kofflard`), **construction** (`E:\repos\MainConstruction\construction`, its submodule `Brinell`). Consumers are changed only in phase 9, after the merge.
- Start of the full plan: only when the owner says so. Phase 0 may run now.

## Beside construction's plan

Construction runs `E:\repos\MainConstruction\construction\.my\bruinet\100-Plan-Test-Changes.md`, which commits to Bruinet's `move` (its submodule `Brinell`) step by step. Its Bruinet steps:

| Step | Bruinet files | State |
| --- | --- | --- |
| T4 write check, "setup" label | `MockApiServer`, Uat failure kind, `failing`, `why` | on `origin/move` (`92d0d47c`) |
| T19 trait names | `Bruinet.Traits` | on `origin/move` (`40f5451b`) |
| T16 scenario engine | feature-file traits, scenario undo, mock request steps (in `Bruinet.Mocking`, `Steps/`), format check | on `origin/move` (`c03119c7`) |
| T18 conversion tools | `draft_feature`, `similar_steps` in `BddMcpServer` | on `origin/move` (`1ec3e616`) |

Rows were held for construction's T4, T16 and T18. All three are on `origin/move` (`1ec3e616`, 2026-11-11) and `reviews` is rebased on it, so nothing is held now. Read what they added before the tasks that meet it: the `setup` failure kind and `failureKind` in the results (phase 3), `Bruinet.Mocking`'s request steps under `Steps/` (R2-T23), and the `draft_feature` and `similar_steps` MCP tools (R2-T94 to T96).
The `voice` rename (D3) reaches construction's `module-videos` skill, used in its per-Module phase 2: it lands only at a submodule bump construction plans for (phase 9).

## Phase 0: base

- [x] R2-T00 Reviews and index committed and pushed (`8b299fb1` after the rebase).
- [x] R2-T02 Packages: `pack.ps1` packs every packable project; `check-packages.ps1` restores and builds a consumer per framework; CI and release run it. R11 P1, P2 (`4af3cbf7` after the rebase).
- [x] R2-T01 Rebase `reviews` onto `origin/move` at `930610b1` (six commits on, among them `Bruinet.Traits`, which `pack.ps1` now packs); pack and check again; push `reviews` with `--force-with-lease`.
- [x] R2-T01b Pin `Microsoft.AspNetCore.WebUtilities` 2.3.14 where WireMock.Net is referenced: GHSA-6527-9fp8-r4x6 (published 2026-11-10) made NuGet's audit fail every build of `Bruinet.Mocking`, on `move` too. Mocking and HTTP replay tests green.
- [x] R2-T03 Design D1, a missing element: null or false. R04 P8, R08 P5. → `designs/D1-missing-element.md`
- [x] R2-T04 Design D2, the MCP tools: renames, results, caps, schema. R03 M1–M11. → `designs/D2-mcp-tools.md`
- [x] R2-T05 Design D3, the command line: exit codes, shared options, `--json`, renames. R10 P1–P8. → `designs/D3-command-line.md`
- [x] R2-T06 Design D4, secrets in recordings and traces, and the SQL hook. R09 P1–P3, P5. → `designs/D4-secrets.md`

## Phase 1: a test must not pass on a fault

- [x] R2-T07 Stride `Read<T>` and `IsTrue` throw on a failed response. R05 P1.
- [x] R2-T08 Stride start-up wait throws `TimeoutException` naming `StartupTimeout`. R02 P2.
- [x] R2-T09 `DesktopApp.WaitForInputIdle` bounded, failure names the app. R02 P3. Launch path not run here (**desk**); unit tests green.
- [x] R2-T10 `FlaUIMauiElement`: 14 reads, 4 action helpers and `TrySetTextValue` catch FlaUI's not-supported exceptions only. R05 P2. **desk**; kofflard.
- [x] R2-T11 Appium classes catch `WebDriverException` with `IsGone`; the two bare catches go. R05 P3. **android**; kofflard.
- [x] R2-T12 Scraper save queue observes faults and reports them through `Changed`. R05 P4.
- [x] R2-T13 Delete `UatControlAssertions` (no caller in Bruinet, Tomorrow, Kofflard, construction). R05 P5.
- [x] R2-T14 Bridge keeps the app's exception message. R05 P6.
- [x] R2-T15 `UatScenarioResults.Record` catches file errors only and traces them. R05 P7.
- [x] R2-T16 The small catches. R05 P10.
- [x] R2-T17 A typed "refused" exception in Core in place of `InvalidOperationException`. R05 P8.
- [x] R2-T18 Check that fails a new bare catch or unfiltered `catch (Exception)` outside an allowed list. R05 P9.

## Phase 2: secrets and what Bruinet writes

- [x] R2-T19 SQL recordings redact by name, guide warns. R09 P1. Needs D4.
- [x] R2-T20 Traces mask password fields and listed controls. R09 P2. Needs D4.
- [x] R2-T21 SQL hook refuses outside a test setup; `begin`/`end` are `POST`. R09 P3. Needs D4.
- [x] R2-T22 MCP `base` refused when it starts with `-`, passed after `--end-of-options`; `review_page` and `record_video` paths confined to the solution. R09 P4.
- [x] R2-T23 HTTP redaction: streamed parts, XML, `Location` query, more default names. R09 P5. Needs D4.
- [x] R2-T24 Voice key file owner-only on Unix. R09 P6.
- [x] R2-T25 Stride pipe `PipeOptions.CurrentUserOnly`. R09 P7.
- [x] R2-T26 VS `watch` opens only `.mp4` under the recordings folder. R09 P8.
- [x] R2-T27 `bruinet sql --connection-from <VAR>`. R09 P9.
- [x] R2-T28 Guide page "What Bruinet writes". R09 P10.

## Phase 3: what a failure says

- [x] R2-T29 "Not found" as the scope sentence, with page and time waited. R01 P1. tomorrow, kofflard, construction read messages only by eye.
- [x] R2-T30 Controls carry their member name (`[CallerMemberName]`) into messages and the log. R01 P2.
- [x] R2-T31 Catalog printed only for a bind error. R01 P3.
- [x] R2-T32 A bind failure writes a result (kind `bind`). R01 P4.
- [x] R2-T33 UATB001 names up to three nearest phrases. R01 P5.
- [x] R2-T34 A closing "what to try" line per failure kind. R01 P6.
- [x] R2-T35 One typed navigation failure on both Html heads; bUnit refuses a foreign host; `PageLoadException` thrown or deleted. R01 P7.
- [x] R2-T36 Locator carried on `ControlAssertionException` and `ScopeNotReadyException`; regex in `UatStepFailure` goes. R01 P8.
- [x] R2-T37 `IsFramework` counts `Bruinet.Uat` as framework; `frames` names the page object member. R01 P9.
- [x] R2-T38 Wording tidy. R01 P10.

## Phase 4: heads agree, contracts bound

- [x] R2-T39 Missing element per D1: generated `ViewBase` or the contract changes; three skips in `HtmlBindings.cs` go. R04 P8, R08 P5. Needs D1. All three consumers.
- [x] R2-T40 Playwright `Submit` uses `requestSubmit()`; `Submit` test on both Html heads. R04 P1.
- [x] R2-T41 bUnit refuses an absent option and `readonly` writes; Selector and EditableText contracts ask. R04 P2. tomorrow (studio on bUnit).
- [x] R2-T42 bUnit bubbles double click, right click, keys; label click and Enter/Space on a button, or `BrowserOnlyException`. R04 P3. tomorrow.
- [x] R2-T43 bUnit reads `aria-disabled`, `aria-checked`; `Text` leaves hidden children out; `docs/guides/bunit.md` lists the rest. R04 P4. tomorrow.
- [x] R2-T44 Html edge tests and `PressKey` into the bindings, both heads. R04 P9.
- [x] R2-T45 Html bindings for Progress, Date, Time, Tab, Scrollable. R04 P11, R08 P1. Done for Progress (Html `ProgressControl` now implements `IProgressControlObject`; no class did before), Date and Time, on both heads. Html's tab and scroll are containers, not control objects, so `TabContract` and `ScrollableContract` wait for MAUI (R2-T47).
- [x] R2-T46 MAUI binds Element, Clickable, Text (MauiHeadless); the 42 `Is…_ReturnsTrue` tests go. R04 P6, R08 P2. Element, Button and Label bound on the Buttons page; on MAUI a hidden view is gone on every head, so the three hidden-control tests skip with why. MAUI's `Label` now declares `ITextControlObject`.
- [x] R2-T47 MAUI binds the eight unbound contracts on MauiHeadless; a contract no head binds is deleted. R08 P1. Six bound on MauiHeadless: Date, Time, Progress (indeterminate skips: MAUI's ProgressBar never is), Expandable (the toolkit's Expander), Swipeable and Refreshable (`RefreshView.IsRefreshing` read the view for the first time). Tab waits for WPF and WinForms (R2-T51); Scrollable needs new MAUI API (R2-T115). No contract is deleted.
- [x] R2-T48 MAUI contracts run once on Windows; per-test skips replace the `Head=MauiHeadless` trait. R04 P5. **desk**. Windows: 100 pass, 16 skip with why, none fail (headless 108 and 8). `[SkipOnFact(heads, why)]` replaces the trait. Fixed on the way: a read-only Entry refuses SetText with ActionRefusedException on every head (Windows threw NotSupportedException); the contract button declares the bridge's DoubleTap; the attribute read is Name, which both heads answer. SwipeView and RefreshView skip on Windows, where they are not addressable.
- [x] R2-T49 MAUI contracts run on Android. **android**. iOS noted as not run (**mac**). Android (Brinell_Android, API 36): 83 pass, 33 skip with why, none fail. Fixed on the way: a double click is the platform's own double-tap gesture (Appium refused two pointer-downs without a pause); Append on Android writes the field's text and the new text, as typing there replaces; the contract button's double tap goes through the sample's ContractVerbSink, since a recognizer swallows a Button's clicks on Android. The reads Android lacks are R2-T116. iOS not run (needs a Mac).
- [x] R2-T50 MAUI `GetAttribute`: one vocabulary, or off the page-object surface. R04 P7. kofflard. One vocabulary, Windows' names (the consumers read those: Kofflard visible and scroll.*, construction Name, AutomationId, ClassName, ControlType), documented on IMauiElement.GetAttribute. MauiHeadless answers them in any case (scroll null: no layout), then bindable properties; Appium answers them as spelled, a lowercase name staying the driver's (XCUITest's name is the AutomationId). Headless 402 of 403 (Paste_LeavesTheClipboardAlone, the shared clipboard, passed alone); Windows contracts and containers pass.
- [x] R2-T51 WPF and WinForms bind the contracts on their fake apps; `ControlTests.cs` copies join. R04 P10, R08 P3. Element, Clickable, Toggle, Expandable and Tab bound on both fake apps over a stateful element (37 each, all pass); Tab is bound for the first time. ControlTests.cs stays in both: its tests count writes and toggles, which the contracts do not, and the two stacks share no element type to join them on.
- [x] R2-T52 Stride and native Android bind the contracts on their fakes. R08 P3. Element, Clickable and Toggle on both: native Android over a stateful element (24, two skipped: no right button or hover on a touch screen), Stride over FakeGame (22, four skipped: its server has a click only). All pass.
- [x] R2-T53 Call model tests move to `Bruinet.Core.Tests`; per-stack files keep only their own. R08 P4. Then `mutate.ps1 -Library Bruinet.Core -Mutate '**/Calls/*.cs'`. R08 P12. Core's CallModelTests on a fake host holds the six; WPF's and WinForms' files went, native Android's three. PredicateWait, ScopeReadiness and Collection tests stay per stack: they read each stack's own element. mutate.ps1 on Calls (the files changed since move, its default): 147 mutants, 48 killed, 96 survived, 15 %: Core's own tests still cover the call model thinly.
- [x] R2-T54 The 35 Moq `Is…` tests in `Bruinet.Blazor.Tests` go once the binding covers them. R08 P6. All 35 went: each read the shared view base's presence answer, which Html's ElementContract proves on bUnit and Chromium; none proved an argument or a call count. SelectByValue's Moq test gives its select the option R2-T41 now asks for.
- [x] R2-T55 UI suite duplicates joined (WinForms roles, Html/Blazor visible and enabled). R08 P7. WinForms: ContainerControlTests folded into InputControlTests (its progress and volume tests kept), one role theory and one clear test left. Html's ButtonControlTests went; Blazor keeps the counter clicks without the visible and enabled checks. The WinForms suite: 47 of 48, the one failing before too (R2-T117).

## Phase 5: waits

- [x] R2-T56 Stride screenshot: wait for foreground and one frame, or read the back buffer. R02 P1. Waits for the window in front (at most 1 s), then one composed frame (DwmFlush); the back-buffer read was not taken.
- [x] R2-T57 Nine MAUI `Thread.Sleep` loops onto `Settle.Until`; a "reads agree" variant. R02 P4. **desk**, **android**. Settle.Read and Settle.Still (two looks agree) join Settle.Until; the nine loops use them; no sleep is left in MAUI's device heads. Run on Windows and Android with the contracts.
- [x] R2-T58 File dialog close inside the budget. R02 P5. The close waits in what the call has left.
- [x] R2-T59 Delete `TimeoutDefaults`, `ElementFind`, `ElementState`. R02 P6. Gone, with their With parameters; a changes.md row. The table's 24 lost \b in its regexes restored on the way.
- [x] R2-T60 `Animation` renamed `ScrollThrottle` or its summary fixed. R02 P7. Summary fixed rather than renamed: the name is in consumers' settings files.
- [x] R2-T61 Fixed windows and launch waits scale with `TimeoutSettings.Scale`. R02 P8. CallBudget.Within scales every fixed window; CallBudget.Scaled for the launch and attach waits.
- [x] R2-T62 `StartTimeout` on the desktop stacks' options. R02 P9. WPF and WinForms options; DesktopApp.Launch takes it. MAUI's Windows launch keeps the default, scaled.
- [x] R2-T63 `WaitHelper.Pause` internal. R02 P10. Already internal; CollectionObjectBase keeps its own pause, listed as a poll in src/.editorconfig.
- [x] R2-T64 One retry for the breakpoint file. R02 P11. BddBreakpoints.ReadShared and WriteShared, used by the Presenter. The Visual Studio extension is net48 and keeps its copy.
- [x] R2-T65 Poll, not sleep, in `run-android-tests.ps1` and the VS Code test. R02 P12.
- [x] R2-T66 Banned-API rule for `Thread.Sleep` and `Task.Delay` in `src/`, allowed files listed. R02 P13. BannedSymbols.Src.txt for the repository's src/ projects; src/.editorconfig lists the 23 allowed files by kind.

## Phase 6: CI, tests and quality

- [x] R2-T67 Six unrun test projects into `coverage.ps1`; `Bdd.Workspace.UITests` under `-Browser`; a test fails when a project is in no list. R08 P8. Seven projects added to coverage.ps1 (Contracts, Logging, Mocking.Http, Traits, MauiHeadless.Tests and its MSTest and NUnit runs; 277 tests, all pass), Bdd.Workspace.UITests under -Browser. SolutionTests.EveryTestProject_IsRun_OrNamedAsNotRun names the eleven that run elsewhere or are libraries.
- [x] R2-T68 Guard scripts fail when `git diff` fails (exit 2). R10 P13. Each git diff checked: exit 2 with the cause, tried on a base that does not exist.
- [x] R2-T69 Test guard and API check run on pushes too. R12 P7. The test-guard job runs on a push as well, against the push's before; a push that opens a branch is skipped. CI's push trigger stays main only (Actions minutes are scarce), so this guards what CI already runs.
- [x] R2-T70 Tests a tier too high moved down. R08 P9. The Presenter's service tests move to a new Bruinet.Presenter.Tests (86, in coverage.ps1), apart from its UAT suite; FixtureInspectorTests stays there, as it inspects that suite's fixture. TestTimingReportTests already runs (the MAUI suite runs headless in CI) and TestTimings is internal to it; Uia's ContractTests runs in coverage.ps1's default list. Both left.
- [x] R2-T71 Probes behind an opt-in attribute or deleted. R08 P10. Five probes are ProbeFact (Maui.UITests has its own file, Uia.Tests its own copy); the bUnit one went with the spike. The WinForms password test asserts the sample's status, which now says the password's length; the empty LoginPage one is gone.
- [x] R2-T72 Leftovers: `Bruinet.Sql.Spike.Tests`, bUnit `SpikeTests.cs`. R08 P11. Sql.Spike.Tests deleted with its recordings (out of the solution and coverage.ps1); bUnit SpikeTests keeps its two named tests; the skipped RightClick_ReturnsPageInstance is gone. The .my citations were left: .my is the owner's.
- [x] R2-T73 Runner tests with the runner; binder and formatter tests for the survivors. R08 P12, P13. The runner's two classes stay in Uat.Tests: both need Uat types (RunnerProcessFixture, UatScenarioResults). mutate.ps1 gives Bruinet.Bdd both test projects, which took BddBinder from 29 to 67 % (the binder's callable tests are in Uat.Tests); a block test covers CheckBlocks' messages and lines; BddDiagnosticsFormatterTests (5) tests every formatter.
- [x] R2-T74 Legacy globalconfig narrowed to `Bruinet.Mocking.Data` or deleted. R12 P1. Legacy for Bruinet.Mocking.Data only; the net48 and netstandard-only projects build without it.
- [x] R2-T75 A reason per disabled rule, or AD-033 allows one per group. R12 P2. AD-033 allows a group of like rules under one comment with their why (the performance hints).
- [x] R2-T76 Code style: rules raised to `warning`, or "code style" out of AD-033. R12 P3. Decide with the owner. Decided: code style is not an error, and AD-033's first sentence says so; raising .editorconfig's rules would be hundreds of findings with no defect behind them. For the owner to overturn.
- [x] R2-T77 BRUINET001 out of `NoWarn`; BRUINET010 scoped to `src` and `tools`. R12 P4. BRUINET001 in Bruinet.Mocking.Tests' own .globalconfig; BRUINET010 off for src and tools only, the 18 test and sample projects that use Bdd's engine opting in in their own files.
- [x] R2-T78 AD-033's counts and restores corrected. R12 P5. AD-033 says 19 methods; the two file-wide pragmas scoped to their lines with a restore (SpatialReplay, HeadlessBehaviors).
- [x] R2-T79 CA1707, CA1051, MA0025 tried for `src`. R12 P6. CA1051 and MA0025 off for test projects only (src clean; the MAUI sample's converters now throw NotSupportedException); CA1707 stays off everywhere, its reason now naming Win32's HRESULT names in Uia.Contracts.

## Phase 7: packages, command line, MCP tools

- [x] R2-T80 One package list: README's table is the packable projects. R11 P3. README's table is the packable projects: Bdd.Abstractions, Mocking.Http and Contracts added; Bdd.Workspace, Scraper and Presenter out, with a line on how they reach a user.
- [x] R2-T81 xUnit declared by Core and Uat, or `ScreenshotTestAttribute` leaves Core. R11 P4. ScreenshotTestAttribute moved to Bruinet.Maui.Testing, the one stack that uses it and declares xUnit; Core has no test framework at all (declaring xUnit in Core broke AD-021's 'Bdd reaches no xUnit'). Uat declares its xUnit packages. Its test moved to Maui.Tests.
- [x] R2-T82 Readme and tags per package. R11 P6. A README.md per packable project (what it is, how to add it, what to name with it), packed through PackageReadmeFile; PackageTags per project on the shared bruinet;ui-testing;testing. Checked in a packed nupkg.
- [x] R2-T83 Appium out of `Bruinet.Maui`. R11 P7. kofflard. Appium.WebDriver out of Bruinet.Maui: nothing used its types (the driver factory reaches Appium by reflection); headless and Windows suites no longer get Selenium.
- [x] R2-T84 The game free of the test client (`Bruinet.Stride.Automation`). R11 P8. The wire types (Bruinet.Stride.Communication) are their own package, Bruinet.Stride.Protocol, same namespace; Bruinet.Stride.Automation, in the game, references it alone, not the client and not Core.
- [x] R2-T85 `Bruinet.Cli` slimmer. R11 P9. Not done: leaving Playwright's Node driver to first use needs a driver download Playwright .NET does not offer, and a per-platform SqlClient needs .NET 10's RID-specific tool packages (ToolPackageRuntimeIdentifiers); both need trying on Windows, Linux and macOS. Proposed as its own item.
- [x] R2-T86 Frameworks and feeds told truly in README and `VERSIONING.md`. R11 P10. README lists each package's frameworks as its project has them (fourteen on net8 to net10, fourteen net10 only, six Windows, Mocking.Data's netstandard2.0 and net48) and names the feed; VERSIONING.md is rewritten against release.yml and ci.yml, 34 lines for 438.
- [x] R2-T87 `controls generate` writes only changed files; the `Create….Bat` scripts count. R11 P11, R10 P10. GenerateToFile writes only when the content changed, compared without line endings; controls generate prints the files it wrote on stdout and 'N of M file(s) changed' on stderr, which the Create….Bat scripts show.
- [x] R2-T88 Command line per D3: exit codes, Ctrl+C, what 2 covers. R10 P1–P3. Needs D3. tomorrow (`Select-Tests.ps1`, `Update-BruinetMcp.ps1`). Per D3: System.CommandLine's handler off; every command's exception ends as one 'bruinet: …' line and exit 2 (SQL, JSON, file and process failures in a sentence), the stack only with BRUINET_DEBUG=1; Ctrl+C exits 130; voice with a broken script exits 1; controls generate's catch-all and stack trace gone, its progress on stderr.
- [ ] R2-T89 Command line per D3: renames, shared `--solution`, `--json`, stderr for progress. R10 P4, P6–P8. Needs D3. tomorrow, construction.
- [ ] R2-T90 `voice` asks before spending or deleting (`--yes`). R10 P5.
- [ ] R2-T91 `cli.md` and AD-028 match the tree; a test fails on a command with no row. R10 P9.
- [ ] R2-T92 `timings.ps1` becomes `bruinet tests timings`; `select-tests.ps1` reads `--json`. R10 P11, P12.
- [x] R2-T93 Dead scripts deleted: `samples/TestRunner.ps1`, `start-appium.ps1`, `.github/PIPELINE.md`; decide `run-android-tests.ps1`, `repeat-run.ps1`. R10 P14. Deleted samples/TestRunner.ps1, tools/Scripts/start-appium.ps1 and .github/PIPELINE.md (nothing named them; construction has its own start-appium). Kept run-android-tests.ps1 (used for R2-T49) and repeat-run.ps1 (docs/run/app-scripts.md gives it to consumers).
- [ ] R2-T94 MCP per D2, no renames: suite that cannot load is a row, nulls skipped, `line` through `ScenarioLine`. R03 M11, M4, M6. Needs D2.
- [ ] R2-T95 MCP per D2: one cap rule. R03 M3. Needs D2.
- [ ] R2-T96 MCP per D2: renames, one results tool, `why` reads only, one name per parameter and verb, schema, instructions. R03 M1, M2, M5, M7–M10. Needs D2. tomorrow (9 files), construction (18 files).

## Phase 8: docs, skills, instructions (after the renames)

- [x] R2-T97 Docs findings 2–6 by hand: quick start, Selenium, links, paths, old names. R06 P1. Findings 2 to 6 fixed: the quick start's page object compiles to today's API; Selenium is out of the docs, as no web head uses it, and WebDriverManager out of the package list; the docs2 links, the .my index, a dead anchor and the Uat paths of Bdd files fixed; the internal driver members gone from troubleshooting; the four quoting samples match their source.
- [x] R2-T98 `docs/controls/Shell.md` deleted or rewritten. R06 P2. Deleted: every sample on it failed and no page linked it.
- [x] R2-T99 Docs check in the test step: links, paths, `bruinet` lines. R06 P3. DocsTests (Core.Tests): every relative link and repository path in README.md and docs/ exists. DocsCommandLineTests (Cli.Tests): the 29 bruinet lines parse against the command tree. Old names are left to the review's one-off search: the change list's Find patterns match too much prose.
- [x] R2-T100 Docs samples from compiled files; AD-033 row. R06 P4, P5. The mechanism: tests/Bruinet.Docs.Samples (compiled, never run), a sample marker per block, DocsSamplesTests, tools/Scripts/update-doc-samples.ps1, an AD-033 row. Only the new Html quick start uses it so far; moving the other 61 blocks is R2-T118.
- [x] R2-T101 A quick start per stack, compiled in CI. R11 P5. Html's quick start (docs/getting-started/quick-start-html.md): feed, project, one package and the browser install, a page object and test from compiled files. The other stacks' are R2-T118.
- [x] R2-T102 Skills and instructions: names gone or wrong (R07 first table). Every row of the first table fixed against the code: RouteUnavailableException (and AD-005 with it), EqualTo, bruinet controls generate, no docs2 or .my index, no BRUINET_ALLOW_POINTER_INPUT, the sample apps' own net10.0 target, the Todo web suites' bUnit head; the convert-control worked example cut.
- [x] R2-T103 Skills: the six disagreeing rules made one, per the owner's answers to R07's five questions. One statement per rule, decided for the owner with reasons: RouteUnavailableException; R5 (a class with no Core methods is a plain .cs; the 38 empty templates are debt); copilot-instructions.md a pointer to AGENTS.md; the scenario-or-UI-test rule once, in AGENTS.md; no .my index. Run* allowed in an alias, not in a Core method (ControlObjectAnalyzer); a raised timeout with a comment; summaries on public and protected members only; one command list; three skills and two Claude stubs added.
- [x] R2-T104 Skills: the ~1,400 words that can go; `copilot-instructions.md` per the owner's answer. About 870 words cut (the review estimated 1,400; moving copilot's own rules into AGENTS.md took some back): the repeated scenario sections, the never-list table, maui-control's contract summary and proof, the worked example.

## Found while working

- [ ] R2-T109 `TOD.08.8 Offline disables Sync…` in `Bruinet.Samples.Todo.Web.Uat` fails in a full run on the bUnit head ("Last Sync" reads "Synced 10:00", expected "Offline") and passes alone, with and without R2-T15: state left by an earlier scenario. Found 2026-11-11.
- [ ] R2-T110 `ArchitectureTests.OnlyBruinetUat_DeclaresUatNames` fails on `move` since `039d9b1b`: `Bruinet.Traits`' `UatAttribute` (`TraitAttributes.cs:52`) is a `Uat` name outside `Bruinet.Uat` (AD-021). Construction's plan drops `[Uat]` in its T10; until then it is red in `Bruinet.Core.Tests`. Not ours to change: told to the owner 2026-11-11.
- [ ] R2-T111 Sixteen MAUI Windows UI tests fail on this machine with and without R2-T10 (2026-11-11): `ProductCollectionTests` (9, "No item at index 0 … materialized items: 0"), `RowTapTests` (2, `ItemTemplateContext` has no InvokePattern), `MediaElementTests` (5, `TimeRemainingElement` not visible); `ItemWhere_ScrollsToFindOffscreenRow` failed once without it and passed with it. 415 of 434 pass.
- [ ] R2-T112 45 of 382 MAUI tests fail on the Android emulator (`Brinell_Android`, API 36) with R2-T11, and the 37 tests of five of those classes fail the same way without it (26 failed, 11 passed both times): Carousel, RatingView, MediaElement, DrawingView, Picker and Entry contracts, nested refresh, paged list. Found 2026-11-11; not investigated.
- [ ] R2-T113 A step test in `Bruinet.Cli.Tests` (`StepTests.RunOfTheFile_…`, about 12 s) fails now and then while other builds run, and passes alone and on the next run; timing on a busy machine, noted, not chased (2026-11-11).
- [ ] R2-T114 `Bruinet.Cli.Tests`' config and MCP tests read the Todo sample's built suite (`samples/Todo/tests/Bruinet.Samples.Todo.Uat`) and fail with empty lists when it was built against an older Core or Bdd; the test project does not build it. Fold into R2-T67 (a CI step builds it first) or R2-T94 (a suite that cannot load is a row). Found 2026-11-11.
- [ ] R2-T115 MAUI `ScrollView` implements `IScrollableControlObject` (top, end, by, position, can-scroll, on every MAUI head) and binds `ScrollableContract`; until then no head binds it. Found in R2-T47.
- [ ] R2-T116 The Android head's missing reads, skipped in the MAUI contracts with this id: a Picker's items and selection, a ProgressBar's progress, an Entry's read-only state (so a write to it is not refused), a RefreshView's state and pull, a SwipeView's top and bottom items, and dropping focus. Found in R2-T49; the Picker part is R2-T112's too.
- [ ] R2-T117 `Bruinet.WinForms.UITests`' `DateTimePicker_SetDate_SetsDateCorrectly` fails on this machine at 86995566 and after (2026-11-11): the picker "accepted SetDate and did not take it", reading today's date. Not investigated.
- [ ] R2-T118 The docs' other 61 C# blocks from compiled files (R2-T100's mechanism), and a compiled quick start for MAUI, WPF, WinForms, native Android, Stride and UAT (R2-T101 did Html).
- [ ] R2-T119 A slimmer Bruinet.Cli (R2-T85): RID-specific tool packages (ToolPackageRuntimeIdentifiers) for SqlClient's native files, and Playwright's Node driver fetched at the Scraper's first use; to try on Windows, Linux and macOS.

## Phase 9: consumers, after the merge into `move`

- [ ] R2-T105 tomorrow: submodule to the merged `move`; skills, CLAUDE.md, `Select-Tests.ps1`, `Update-BruinetMcp.ps1` for the renames; studio suite on bUnit (smoke and touched).
- [ ] R2-T106 kofflard: submodule moved; MAUI suite on headless, then **desk** and **android** smoke.
- [ ] R2-T107 construction: its `Brinell` submodule (has its own commits on `move`) moved; instructions for the tool renames; headless smoke.
- [ ] R2-T108 Index `00-index.md` updated: what was done, what is left, what stays unrun (iOS).

## Open with the owner

- D1–D4: approved by the owner as proposed on 2026-11-11; their tasks are no longer blocked on them.
- R07's five questions (exception name, R5, `copilot-instructions.md`, scenario-or-test rule, `.my` index): my proposals stand in the review.
- R2-T76 code style, R2-T93 two scripts, Selenium (`WebDriverManager` too).
- Pushing `reviews` to the jonkeda mirror: blocked by the permission check on 2026-11-11, not retried.
