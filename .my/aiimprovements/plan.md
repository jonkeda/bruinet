# AI improvements: plan

Status: **plan**, 2026-11-01. Picked up one item at a time, in the order below. Sources:
[ten-agent-proposals](../proposals/ten-agent-proposals.md) (AP1-10), [ten-proposals](../proposals/ten-proposals.md) (P1-10),
[improvements](../improvements/README.md), [bdd-gaps](../bdd-gaps/design.md). Checked against the tree on `move`, 2026-11-01.

## Outcome

Ordered by how much each item helps an agent create, edit, debug UI tests or fix the app under test. The first finding drives
the order: the language server can already run, step, screenshot and rename (`bruinet/run/*`, `bruinet/step/*`,
`bruinet/lastRuns`, `textDocument/rename`, `bruinet/scraper/renamePageObject`), but the MCP server (`BddMcpServer`) exposes
none of it. Its `run_test` runs only a test recording's drafts. An agent today cannot run an arbitrary scenario and read why it
failed. Wave 1 closes that loop, mostly by wrapping what exists.

## Order

| # | Item | Source | Size | Builds on |
| --- | --- | --- | --- | --- |
| AI1 | `run_scenario` and `last_runs` MCP tools | AP1 | S | `bruinet/run/start`, `bruinet/runner/warm`, `bruinet/lastRuns` |
| AI2 | Failures as structured data | AP2, bdd-gaps D1's run fields | M | AI1, `__result.json`, `BddStepResult` |
| AI2b | App code coverage per scenario | the coverage session | M | AI1, the coverage session's collection |
| AI3 | `tests_affected` MCP tool | test selection | S | `bruinet tests affected --json` |
| AI4 | Step session via MCP, with page state | AP1, AP6 | M | `bruinet/step/start`, `move`, `screenshot` |
| AI5 | `rename_step`, `rename_page_object` MCP tools | AP7 (part 1) | S | `textDocument/rename`, `bruinet/scraper/renamePageObject` |
| AI6 | App telemetry per step | AP4 | M | AI2 |
| AI7 | Map from UI element to source line | AP3 | M | page object locators |
| AI8 | Per-step trace on every head, text first | P2 | L | AI2, AI4's page state |
| AI9 | Since-last-green failure diff | P1 | L | AI8, touch map |
| AI10 | Put the app in a state | AP8 | M | test sets, service head |
| AI11 | HTTP and model-call replay on WireMock.Net | P4 | M | SQL replay's conventions |
| AI12 | Change impact before running | AP9 | M | AI3, touch map, page object locators |
| AI13 | Locator fix proposed as a patch | P3 | M | AI8 snapshots, `find_similar_elements` |
| AI14 | Safe C# refactors on a Roslyn workspace | AP7 (part 2) | L | AI5, `find_step_usages`' index |
| AI15 | Reproduce first, then fix | AP10 | M | AI1, AI2, AI4, `resolve_actions` |
| AI16 | Plan, generate and heal skills | AP5 | M | AI1-AI5, AI13, AI15 |
| AI17 | Flake detection with a cause | P6 | M | AI9 |
| AI18 | Accessibility on every head | P8 | M | MAUI's `accessibility-audit.md` |
| AI19 | Agent-run exploratory testing | P10 | L | AI4, AI2, AI18, `explore_app` |
| AI20 | Mutation testing for scenarios | P9 | L | bUnit, MauiHeadless, touch map |

Waves: AI1-AI5 (with AI2b) close the loop (run, read, see which code ran, step, rename). AI6-AI9 let the agent see inside. AI10-AI14 make each loop cheaper or
safer. AI15-AI20 package it into workflows.

## Wave 1: close the loop

### AI1. `run_scenario` and `last_runs`

- `run_scenario {path, line?, suite?}`: warms the runner (`bruinet/runner/warm`), runs via `bruinet/run/start`, waits for `bruinet/run/ended`; returns per scenario `{address, status, failedStep?, message?, durationMs}`. A file without `line` runs every scenario.
- `last_runs {path}`: `bruinet/lastRuns` as is.
- Results are written as the gate writes them, marked `run` (already so).
- Done when: an agent runs TOD.02 on the web suite and on bUnit from MCP and reads the outcome; a test in `tests/Bruinet.Cli.Tests` drives both tools.

### AI2. Failures as structured data

- `get_failure {path, line}`: from the newest result of that scenario. Fields: `kind` (locator-not-found, assertion, timeout, app-exception, setup, bind), `step` (text, `file:line`), `binding` (class.method, `file:line`), `member` (page object member, `file:line`), `expected`, `actual`, `screenshot` (path), `message` capped at ~2 KB, `stack` top 5 frames from user code.
- The run must add to `BddStepResult` and `__result.json`: `Started`, `Duration` per step (bdd-gaps D1's "what the run must add first"), and the failing `kind`.
- `run_scenario` returns `get_failure`'s object inline for a failed scenario.
- Done when: each `kind` has a unit test with a recorded result, and the output for a real failure is under 500 tokens.

### AI2b. App code coverage per scenario

- Why: an agent proves a new scenario reaches the code it fixed, sees why a scenario passed without reaching a branch, and finds changed lines no scenario runs.
- `run_scenario {..., coverage: true}`; then `coverage {path, line, files?}` returns hit and missed line ranges per file, only for `files` or, without them, the working diff (`TodoStore.cs: 42-48 hit, 51-53 missed`). Never the whole report.
- Per scenario, not per run: in-process heads (bUnit, MauiHeadless, service) reset or snapshot the counters between scenarios; browser and device heads collect from the app's process with `dotnet-coverage` (server mode, a snapshot per scenario). Coverlet writes only at process exit, so it does not fit here.
- Opt-in per call: instrumentation slows a run.
- The collection itself comes from the coverage session's work; this item adds the per-scenario snapshots, the result beside `__result.json` (`<scenario>__coverage.json`), and the MCP tool. Read that session's outcome before designing.
- Later: per-scenario app lines feed the touch map, making AI3 and AI12 exact for app code.
- Done when: a Todo scenario run with coverage reports the lines of `TodoStore` it hit and missed, on bUnit and on the web head.

### AI3. `tests_affected`

- `tests_affected {suite?, base?, files?}`: `bruinet tests affected --json` in process; returns tests with why, and the `--filter`.
- Done when: after a change to a page object an agent gets the scenarios to run and passes them to AI1.

### AI4. Step session via MCP

- Tools: `step_start {path, line}`, `step_move {move: next|continue|stop|to-line}`, `step_screenshot`, `step_state`.
- `step_state` is new: the page object in view, its controls with kind and current value, as an outline (the `get_page_snapshot` style), for every head; no screenshot needed.
- One paused session per MCP process; a second `step_start` stops the first.
- `--break-at` on `run_scenario` (AP6) maps to `step_start` plus `step_move to-line`.
- Done when: an agent pauses before a failing step, reads `step_state`, and sees the wrong value without running again.

### AI5. Renames via MCP

- `rename_step {path, line, newPhrase, apply?}`: `textDocument/rename`'s `WorkspaceEdit` as a unified diff; `apply` writes it, then runs `validate_scenario` on every changed file; on failure writes nothing.
- `rename_page_object {project, page, newName, apply?}`: `bruinet/scraper/renamePageObject`, same diff-then-apply rule.
- Done when: renaming a TOD step from MCP leaves every Todo suite binding (`validate_scenario` green).

## Wave 2: see inside

### AI6. App telemetry per step

- An `Activity` per step in `BddExecution`; a test-host `ILoggerProvider` and an exception hook tag app logs with the step's id; browser console errors from the Playwright head.
- Stored beside the result (`<scenario>__steplog.json`); `get_failure` gains `appEvents` (errors and warnings of the failing step, capped); `step_log {path, line, step}` returns all.
- Done when: a Todo scenario failing on a thrown `SqlException` shows it in `get_failure`.

### AI7. Map from UI element to source line

- Html/Blazor: a Debug-only build step adds `data-bruinet-src="<file>:<line>"` to elements rendered by `.razor` files; MAUI/WPF: `x:Name` plus the XAML file from the generated `.g.cs`; WinForms: the designer file.
- `where_is {suite, page, control}`: app file and line, page object file and line.
- `get_failure` gains `appSource` for the failing control.
- Open: whether Blazor's Razor compiler can add the attribute without a source generator per app; a spike decides.

### AI8. Per-step trace, text first

- Per step: `step_state` before and after (AI4's outline), the control driven, waits with durations, AI6's events; one JSON per scenario in `traces/`.
- Agents read it through `get_trace {path, line, step?}`; the viewer for people (Presenter, VS Code, HTML) comes later, from the same file.
- Written on failure by default; `BRUINET_TRACE=all` writes it for passes too (AI9 needs passes).

### AI9. Since-last-green diff

- `why {path, line}`: the newest failing trace against the newest passing trace of the same scenario; the first step whose state, SQL or events differ; the files changed between the two runs' commits that the touch map ties to that step; a `kind` of page-changed, data-changed or timing-only.
- CLI `bruinet why <file>:<line>` and a line in `summary.md`.

## Wave 3: cheaper and safer loops

### AI10. Put the app in a state

- States are declared in the test project (`[BddState("signed in as {user}")]` on a method using test sets or the service head); `list_states`, `set_state {name, args}`; a `Given` may name a state.
- Guard: only declared states, only against the suite's own host and database.

### AI11. HTTP and model-call replay on WireMock.Net

- As P4: WireMock.Net in proxy mode records, replay serves mappings per scenario; base URLs pointed by the suite's host; a missing recording fails naming the call; a model-call matcher preset ignoring request ids, `temperature` and `seed`; `--refresh`.
- First: a spike on streamed answers (server-sent events).

### AI12. Change impact before running

- `impact {base?, files?}`: AI3's affected tests plus, for changed `.razor`/XAML, each page object locator checked against the changed markup; returns "no longer matches" per member.

### AI13. Locator fix as a patch

- On locator-not-found: the last passing trace's node for that member, `find_similar_elements` on the failing state, the best match as a page object patch with a score; the test still fails.
- `get_failure` gains `proposedFix` (diff); `apply_fix {id}` writes it and reruns via AI1.

### AI14. Safe C# refactors

- A Roslyn workspace loaded once per MCP process (`Microsoft.CodeAnalysis.Workspaces.MSBuild`, `Microsoft.Build.Locator`), refreshed on file changes.
- `rename_member`, `move_control`, `split_page`, `extract_control`: Roslyn's `Renamer` for C#, plus the phrase, binding and feature file updates from `find_step_usages`' index; diff first; apply runs `check_code` and `validate_scenario`, writes nothing on failure.

## Wave 4: workflows

- **AI15 Reproduce first:** `repro_from_recording {session}` via `resolve_actions` and `draft_test`; the handover records red on the old commit and green on the new (AI1), with `review_page`.
- **AI16 Skills:** planner, generator, healer as skills and agent definitions shipped with the CLI; they use only AI1-AI5, AI13, AI15.
- **AI17 Flake detection:** a results history per suite beside `durations.json`; a scenario both passing and failing on one commit is flagged with AI9's diff; `run_scenario {repeat: n}`.
- **AI18 Accessibility on every head:** axe-core on web and bUnit, UI Automation on Windows, ATF on Android; findings named by page object and control; a baseline file; `get_failure` kind `accessibility`.
- **AI19 Exploratory agent:** `explore_app` extended to drive page objects through AI4, report crashes, console errors, hangs and AI18 findings, each with a draft scenario; budget in steps and minutes.
- **AI20 Mutation testing:** mutations in bUnit and MauiHeadless only; the touch map limits scenarios per mutation; a report of controls no Then-step checks.

## Not in this plan

| Item | Why not here |
| --- | --- |
| P5 visual snapshot review | a person approves images; little for an agent until AI8 exists |
| P7 sharding | wall-clock in CI, not an agent's loop |
| bdd-gaps D1 Cucumber Messages | parked until a reader is named; AI2 takes its run fields |
| improvements 1-8 | their own plan; item 1's status column also tells agents which heads to trust, an hour to do |

## Per item

Built with the `ai-item` skill (`.github/skills/ai-item/SKILL.md`): design, build test-first, checks, agent check,
review, close. A design note in this folder first for AI2, AI2b, AI4, AI7, AI8, AI11, AI14; the others go straight to the
build. Agent checks live in `checks/`, reviews as `<id>-review.md`.

## Status

| Item | Status | Commit | Learned |
| --- | --- | --- | --- |
| AI1 | open | | |
| AI2 | open | | |
| AI2b | open | | |
| AI3 | open | | |
| AI4 | open | | |
| AI5 | open | | |
| AI6 | open | | |
| AI7 | open | | |
| AI8 | open | | |
| AI9 | open | | |
| AI10 | open | | |
| AI11 | open | | |
| AI12 | open | | |
| AI13 | open | | |
| AI14 | open | | |
| AI15 | open | | |
| AI16 | open | | |
| AI17 | open | | |
| AI18 | open | | |
| AI19 | open | | |
| AI20 | open | | |

Status: `open`, `design`, `gate` (waiting on the owner), `build`, `check`, `review`, `done`.
