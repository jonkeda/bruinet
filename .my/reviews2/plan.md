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
- [ ] R2-T18 Check that fails a new bare catch or unfiltered `catch (Exception)` outside an allowed list. R05 P9.

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
- [ ] R2-T36 Locator carried on `ControlAssertionException` and `ScopeNotReadyException`; regex in `UatStepFailure` goes. R01 P8.
- [ ] R2-T37 `IsFramework` counts `Bruinet.Uat` as framework; `frames` names the page object member. R01 P9.
- [ ] R2-T38 Wording tidy. R01 P10.

## Phase 4: heads agree, contracts bound

- [ ] R2-T39 Missing element per D1: generated `ViewBase` or the contract changes; three skips in `HtmlBindings.cs` go. R04 P8, R08 P5. Needs D1. All three consumers.
- [ ] R2-T40 Playwright `Submit` uses `requestSubmit()`; `Submit` test on both Html heads. R04 P1.
- [ ] R2-T41 bUnit refuses an absent option and `readonly` writes; Selector and EditableText contracts ask. R04 P2. tomorrow (studio on bUnit).
- [ ] R2-T42 bUnit bubbles double click, right click, keys; label click and Enter/Space on a button, or `BrowserOnlyException`. R04 P3. tomorrow.
- [ ] R2-T43 bUnit reads `aria-disabled`, `aria-checked`; `Text` leaves hidden children out; `docs/guides/bunit.md` lists the rest. R04 P4. tomorrow.
- [ ] R2-T44 Html edge tests and `PressKey` into the bindings, both heads. R04 P9.
- [ ] R2-T45 Html bindings for Progress, Date, Time, Tab, Scrollable. R04 P11, R08 P1.
- [ ] R2-T46 MAUI binds Element, Clickable, Text (MauiHeadless); the 42 `Is…_ReturnsTrue` tests go. R04 P6, R08 P2.
- [ ] R2-T47 MAUI binds the eight unbound contracts on MauiHeadless; a contract no head binds is deleted. R08 P1.
- [ ] R2-T48 MAUI contracts run once on Windows; per-test skips replace the `Head=MauiHeadless` trait. R04 P5. **desk**.
- [ ] R2-T49 MAUI contracts run on Android. **android**. iOS noted as not run (**mac**).
- [ ] R2-T50 MAUI `GetAttribute`: one vocabulary, or off the page-object surface. R04 P7. kofflard.
- [ ] R2-T51 WPF and WinForms bind the contracts on their fake apps; `ControlTests.cs` copies join. R04 P10, R08 P3.
- [ ] R2-T52 Stride and native Android bind the contracts on their fakes. R08 P3.
- [ ] R2-T53 Call model tests move to `Bruinet.Core.Tests`; per-stack files keep only their own. R08 P4. Then `mutate.ps1 -Library Bruinet.Core -Mutate '**/Calls/*.cs'`. R08 P12.
- [ ] R2-T54 The 35 Moq `Is…` tests in `Bruinet.Blazor.Tests` go once the binding covers them. R08 P6.
- [ ] R2-T55 UI suite duplicates joined (WinForms roles, Html/Blazor visible and enabled). R08 P7.

## Phase 5: waits

- [ ] R2-T56 Stride screenshot: wait for foreground and one frame, or read the back buffer. R02 P1.
- [ ] R2-T57 Nine MAUI `Thread.Sleep` loops onto `Settle.Until`; a "reads agree" variant. R02 P4. **desk**, **android**.
- [ ] R2-T58 File dialog close inside the budget. R02 P5.
- [ ] R2-T59 Delete `TimeoutDefaults`, `ElementFind`, `ElementState`. R02 P6.
- [ ] R2-T60 `Animation` renamed `ScrollThrottle` or its summary fixed. R02 P7.
- [ ] R2-T61 Fixed windows and launch waits scale with `TimeoutSettings.Scale`. R02 P8.
- [ ] R2-T62 `StartTimeout` on the desktop stacks' options. R02 P9.
- [ ] R2-T63 `WaitHelper.Pause` internal. R02 P10.
- [ ] R2-T64 One retry for the breakpoint file. R02 P11.
- [ ] R2-T65 Poll, not sleep, in `run-android-tests.ps1` and the VS Code test. R02 P12.
- [ ] R2-T66 Banned-API rule for `Thread.Sleep` and `Task.Delay` in `src/`, allowed files listed. R02 P13.

## Phase 6: CI, tests and quality

- [ ] R2-T67 Six unrun test projects into `coverage.ps1`; `Bdd.Workspace.UITests` under `-Browser`; a test fails when a project is in no list. R08 P8.
- [ ] R2-T68 Guard scripts fail when `git diff` fails (exit 2). R10 P13.
- [ ] R2-T69 Test guard and API check run on pushes too. R12 P7.
- [ ] R2-T70 Tests a tier too high moved down. R08 P9.
- [ ] R2-T71 Probes behind an opt-in attribute or deleted. R08 P10.
- [ ] R2-T72 Leftovers: `Bruinet.Sql.Spike.Tests`, bUnit `SpikeTests.cs`. R08 P11.
- [ ] R2-T73 Runner tests with the runner; binder and formatter tests for the survivors. R08 P12, P13.
- [ ] R2-T74 Legacy globalconfig narrowed to `Bruinet.Mocking.Data` or deleted. R12 P1.
- [ ] R2-T75 A reason per disabled rule, or AD-033 allows one per group. R12 P2.
- [ ] R2-T76 Code style: rules raised to `warning`, or "code style" out of AD-033. R12 P3. Decide with the owner.
- [ ] R2-T77 BRUINET001 out of `NoWarn`; BRUINET010 scoped to `src` and `tools`. R12 P4.
- [ ] R2-T78 AD-033's counts and restores corrected. R12 P5.
- [ ] R2-T79 CA1707, CA1051, MA0025 tried for `src`. R12 P6.

## Phase 7: packages, command line, MCP tools

- [ ] R2-T80 One package list: README's table is the packable projects. R11 P3.
- [ ] R2-T81 xUnit declared by Core and Uat, or `ScreenshotTestAttribute` leaves Core. R11 P4.
- [ ] R2-T82 Readme and tags per package. R11 P6.
- [ ] R2-T83 Appium out of `Bruinet.Maui`. R11 P7. kofflard.
- [ ] R2-T84 The game free of the test client (`Bruinet.Stride.Automation`). R11 P8.
- [ ] R2-T85 `Bruinet.Cli` slimmer. R11 P9.
- [ ] R2-T86 Frameworks and feeds told truly in README and `VERSIONING.md`. R11 P10.
- [ ] R2-T87 `controls generate` writes only changed files; the `Create….Bat` scripts count. R11 P11, R10 P10.
- [ ] R2-T88 Command line per D3: exit codes, Ctrl+C, what 2 covers. R10 P1–P3. Needs D3. tomorrow (`Select-Tests.ps1`, `Update-BruinetMcp.ps1`).
- [ ] R2-T89 Command line per D3: renames, shared `--solution`, `--json`, stderr for progress. R10 P4, P6–P8. Needs D3. tomorrow, construction.
- [ ] R2-T90 `voice` asks before spending or deleting (`--yes`). R10 P5.
- [ ] R2-T91 `cli.md` and AD-028 match the tree; a test fails on a command with no row. R10 P9.
- [ ] R2-T92 `timings.ps1` becomes `bruinet tests timings`; `select-tests.ps1` reads `--json`. R10 P11, P12.
- [ ] R2-T93 Dead scripts deleted: `samples/TestRunner.ps1`, `start-appium.ps1`, `.github/PIPELINE.md`; decide `run-android-tests.ps1`, `repeat-run.ps1`. R10 P14.
- [ ] R2-T94 MCP per D2, no renames: suite that cannot load is a row, nulls skipped, `line` through `ScenarioLine`. R03 M11, M4, M6. Needs D2.
- [ ] R2-T95 MCP per D2: one cap rule. R03 M3. Needs D2.
- [ ] R2-T96 MCP per D2: renames, one results tool, `why` reads only, one name per parameter and verb, schema, instructions. R03 M1, M2, M5, M7–M10. Needs D2. tomorrow (9 files), construction (18 files).

## Phase 8: docs, skills, instructions (after the renames)

- [ ] R2-T97 Docs findings 2–6 by hand: quick start, Selenium, links, paths, old names. R06 P1.
- [ ] R2-T98 `docs/controls/Shell.md` deleted or rewritten. R06 P2.
- [ ] R2-T99 Docs check in the test step: links, paths, `bruinet` lines. R06 P3.
- [ ] R2-T100 Docs samples from compiled files; AD-033 row. R06 P4, P5.
- [ ] R2-T101 A quick start per stack, compiled in CI. R11 P5.
- [ ] R2-T102 Skills and instructions: names gone or wrong (R07 first table).
- [ ] R2-T103 Skills: the six disagreeing rules made one, per the owner's answers to R07's five questions.
- [ ] R2-T104 Skills: the ~1,400 words that can go; `copilot-instructions.md` per the owner's answer.

## Found while working

- [ ] R2-T109 `TOD.08.8 Offline disables Sync…` in `Bruinet.Samples.Todo.Web.Uat` fails in a full run on the bUnit head ("Last Sync" reads "Synced 10:00", expected "Offline") and passes alone, with and without R2-T15: state left by an earlier scenario. Found 2026-11-11.
- [ ] R2-T110 `ArchitectureTests.OnlyBruinetUat_DeclaresUatNames` fails on `move` since `039d9b1b`: `Bruinet.Traits`' `UatAttribute` (`TraitAttributes.cs:52`) is a `Uat` name outside `Bruinet.Uat` (AD-021). Construction's plan drops `[Uat]` in its T10; until then it is red in `Bruinet.Core.Tests`. Not ours to change: told to the owner 2026-11-11.
- [ ] R2-T111 Sixteen MAUI Windows UI tests fail on this machine with and without R2-T10 (2026-11-11): `ProductCollectionTests` (9, "No item at index 0 … materialized items: 0"), `RowTapTests` (2, `ItemTemplateContext` has no InvokePattern), `MediaElementTests` (5, `TimeRemainingElement` not visible); `ItemWhere_ScrollsToFindOffscreenRow` failed once without it and passed with it. 415 of 434 pass.
- [ ] R2-T112 45 of 382 MAUI tests fail on the Android emulator (`Brinell_Android`, API 36) with R2-T11, and the 37 tests of five of those classes fail the same way without it (26 failed, 11 passed both times): Carousel, RatingView, MediaElement, DrawingView, Picker and Entry contracts, nested refresh, paged list. Found 2026-11-11; not investigated.
- [ ] R2-T113 A step test in `Bruinet.Cli.Tests` (`StepTests.RunOfTheFile_…`, about 12 s) fails now and then while other builds run, and passes alone and on the next run; timing on a busy machine, noted, not chased (2026-11-11).

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
