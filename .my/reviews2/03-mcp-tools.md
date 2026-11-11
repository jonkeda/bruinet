# Bruinet's MCP tools: review and proposals

Written 2026-11-11 at `b2ea4570`. Read from the source; nothing was built.
Seven read-only tools were called on the running server to see real results (`list_suites`, `failing`, `list_steps`, `list_scraped_projects`, `list_recorded_pages`, `list_controls`, `list_recorded_actions`).
Unless a file is named, a line number below is a line of `tools/Bruinet.Cli/Lsp/BddMcpServer.cs`.

## Outcome

The 53 tools are all declared in one list (lines 42 to 94) and answered by one switch (lines 175 to 233), so the surface is easy to read and to change.
The descriptions are honest and mostly say what comes back, and the newer tools cap what they return.
What costs an agent a wrong call is the naming: two tool families (a suite's scenarios, a scraped project's recordings) share one flat list with no prefix, and "page", "test", "recording" and "check" each mean two or three things.
Second is size: twelve tools return a list with no cap, and no result drops its null fields.
Third is the schema: it carries no required list, no enumerations and no read-only mark, so the rules live in prose only.

Nothing is shipped, so every rename below is free today.

## What is good and should stay

- One declaration and one switch; a tool's name, text and parameters are one line.
- A tool that cannot answer says why as its result, often naming the next tool (`get_trace` at 1037, `set_state` at 474 and 488).
- `ScenarioLine` (1103) takes any line of a scenario, because agents give the failing step's line.
- The asking tools answer a preview first and act on a second call (`explore_app`, `create_scraped_project`, `update_scraper_config`, `rename_page_object`, `rename_step`, `delete_results`).
- The failure tools cap their text: `failing` at 50 rows and 300 characters (802, 827), `step_state` at 60 controls (190), `http_calls` at 50 calls (438).
- `run_scenario` answers a failed scenario with its failure, the app's source line and `sinceGreen` in the same call (378 to 381).

## Findings

### 1. Two families in one flat list

Of the 53, 24 work on a suite (`suite`, a feature file's `path` and `line`; lines 42 to 49, 78 to 92 and 94) and 29 on a scraped project (lines 50 to 77 and 93; `project`, a `page` key, a `session`).
Nothing in a name says which, and the same word is used on both sides.

| Pair | What each really is | Lines |
| --- | --- | --- |
| `run_test`, `run_scenario` | A recorded session's drafts by `session`; a feature file's scenario by `path` and `line` | 71, 80 |
| `list_pages`, `list_recorded_pages`, `list_controls` | A suite's page objects; a project's captured pages; a project's page objects and controls | 44, 55, 59 |
| `review_page`, `diff_page`, `capture_page` | A hand-over HTML page with videos; a recorded page's change; a capture in the recording browser | 49, 63, 53 |
| `record_video`, `start_recording`, `search_recordings` | A scenario's video; a Scraper session; a text search in captures | 48, 52, 58 |
| `check_code`, `write_code` | Both work only on a scraped project, though the name reads as general | 72, 66 |
| `draft_test`, `test_brief`, `tests_affected` | The first two are a recorded session's; the third is a suite's test selection | 70, 69, 91 |

- `list_pages` says "pages and their controls" and `list_controls` says "page objects and controls": an agent cannot pick between them from the text (44, 59).
- `run_scenario`'s `replay: record` (80) is a fourth meaning of "record": the HTTP and SQL replay files.
- The server calls itself `bruinet-bdd` (143), though 29 of its tools are the Scraper's.

### 2. The failure tools overlap, and one of them runs

`failing`, `last_runs`, `get_trace`, `why` and `http_calls` all answer "what went wrong", and `run_scenario` already carries most of it.

- `last_runs` (81, 1145) is `failing` for one file with passed rows added, but returns less per failure: no kind, no address, no time, and a 2,000 character message where `failing` gives 300.
- `why` reads as a question but builds and runs the scenario when no failure is traced (884 to 904). The description says so in its last sentence but one; the run takes `run_scenario`'s default ten minutes and `why` has no `timeoutMinutes`.
- `run_scenario`'s `sinceGreen` is `why` in short (835 to 857), so after a failed run `why` is the same answer again with more rows.
- `failing` says "get_trace gives one in full", `run_scenario`'s note says "why gives it in full" (856): two tools are "the full one".
- `get_trace`, `why` and `http_calls` each take `path`, `line` and `suite`, while `failing` returns `File`, `Scenario` and `Address` but no line (819 to 828), so the agent must open the file to find the line before it can ask for more.
- Three are bare nouns or a question word (`failing`, `why`, `coverage`), which sort nowhere near `get_trace` and `last_runs`.

### 3. Twelve lists have no cap

Measured on `tests/Bruinet.Blazor.UITests` (13 recorded pages, 5 page objects): `list_recorded_pages` and `list_controls` each answered about 5 KB. At the same rate a project of 200 pages and 150 page objects gives about 80 KB and 170 KB.

| Tool | What grows | Where |
| --- | --- | --- |
| `list_steps` | Every phrase of the suite unless `query` is given; a control verb per phrase | 1436 to 1463 |
| `list_pages` | Every page with every control | 1466 to 1475 |
| `list_controls` | Every class with every member and locator, referenced projects too | `ScraperService.cs:455` |
| `list_recorded_pages` | Every capture of every page, each with an absolute file path | 214 |
| `find_similar_elements` | Every matching node on every page; a table cell matches thousands | `ScraperService.cs:433` |
| `suggest_controls`, `match_controls` | One row per node of the page | `ScraperService.cs:535`, `:517` |
| `list_recorded_actions`, `resolve_actions` | One row per event of the session | 207, 208 |
| `find_step_usages` | Every step that uses the member | `BddLanguageServer.Usages.cs:68` |
| `last_runs` | Up to 2,000 characters per failed scenario of the file | 1147 |
| `get_trace` | The failed step in full: up to 50 calls and 60 controls | 1053, `UatScenarioTrace.cs:15` |

- The capped tools say so four ways: `More` (`failing`, `step_state`, `http_calls`, `tests_affected`), `MoreFiles` (755), `CodeChangedMore` (855), a text line (`SnapshotOutline.cs:55`).
- `search_recordings` stops at 100 and `diff_page` lists 100 of each kind without saying more exist (`ScraperService.cs:451`, `SnapshotDiff.cs:13`).
- `http_calls` answers `More: 0` where the others answer null (439).
- The limit is `max` on `detect_patterns` and `maxLines` on `get_page_snapshot`; no other tool has one.

### 4. Null fields are sent

The serializer (22) does not skip nulls; only the Step-through answers remove them by hand (672).
A real `list_recorded_actions` row for one page capture is 330 characters with 12 of its 17 fields null. `list_recorded_pages` sends `pageObject: null, changedSinceGenerated: null` for 12 of 13 pages.
`run_scenario` sends `FailedStep`, `Failure`, `AppSource`, `SinceGreen` and `Message` as null for every scenario that passed (372 to 383).

### 5. Paths and lines come in three shapes

- Relative to the solution: `failing`, `run_scenario`, `rename_step`, `coverage`, `http_calls`.
- Absolute: `list_suites` `Config` (1430), `validate_scenario` `File` (1493), `list_controls` `file`, `list_recorded_pages` `file`, `list_scraped_projects` `folder`, `record_video`, `step_screenshot` (568).
- A `file:///` URI with a zero-based line: `find_step_usages` alone (`BddLanguageServer.Usages.cs:62`); every other tool's line is one-based.
- `review_page` resolves `path` and `videos` against the server's working folder, not the solution (1346, 1359); every other tool resolves against the solution (1286).

### 6. `line` means two things

Six tools pass `line` through `ScenarioLine`, so any line of the scenario works (`run_scenario`, `step_start`, `coverage`, `why`, `get_trace`, `http_calls`).
Three do not: `record_video` (1312), `delete_results` (1300) and `rename_step` (1207) take the raw number, and the first two fail unless it is the heading's.
The comment at 1101 records that half of a benchmark's `why` calls failed on exactly this before `ScenarioLine` existed.

### 7. The same parameter, different names and defaults

| Thing | How it differs | Lines |
| --- | --- | --- |
| `suite` left out | "the first suite" (list tools), "its only one" (`list_states`, `set_state`), "every suite" (`failing`), required and may be a folder (`tests_affected`) | 43, 78, 90, 91 |
| Go-ahead | `confirm` (four tools), `apply` (`rename_step`), `dryRun: false` (`delete_results`) | 73, 92, 94 |
| `dryRun` | In `explore_app` it means "links only" and sits beside `confirm`; in `delete_results` it is the go-ahead | 73, 94 |
| An event of a session | `event` (`test_brief`), `number` (`get_page_snapshot`), `fromNumber` and `toNumber` (`diff_page`) | 69, 56, 63 |
| Search words | `query` (`list_steps`), `text` (`search_recordings`); `text` is also a draft's content in `validate_scenario` | 43, 58, 46 |
| `page` | A page key such as `/userform` in the Scraper tools; a page object's name in `set_state` | 55, 79 |
| `build` | False when left out in `run_scenario`, true in `coverage`; always in `record_video`, `run_test`, `why` | 80, 86, 710 |
| `headless` | False when left out in `explore_app`, true in `verify_page_objects` | 271, 230 |
| `timeoutMinutes` | 1 to 120 for a run, 1 to 10 for a step; `record_video` and `run_test` have none and wait forever | 358, 600, 1369 |

- `list_states` and `set_state` say "its only one when left out", but `Suite()` takes the first of several (1515), as the list tools say.
- `run_scenario`'s `trace` accepts `failed` (399), which its description does not name.

### 8. Verbs differ for the same act

- Reading a list: `list_…` (9 tools), `get_…` (3), and bare nouns (`last_runs`, `failing`, `http_calls`, `tests_affected`, `coverage`, `scraped_header`, `control_brief`, `test_brief`).
- Checking: `validate_scenario`, `check_code`, `verify_page_objects`.
- Finding: `find_similar_elements`, `find_step_usages`, `search_recordings`, `detect_patterns`, `match_controls`.
- Making a file: `draft_test`, `draft_page_objects`, `write_code`, `create_scraped_project`, `review_page` (a noun that writes).
- The command line says `bruinet tests affected` and `bruinet scenario …` (AD-028: groups are nouns, commands verbs); the tools say `tests_affected` but `run_scenario`, verb last in one and first in the other.

### 9. The schema says less than the descriptions

- `Schema()` (1555) writes no `required` array. "required" is the last word of a parameter's description, and a missing one is found only by the call failing (1546).
- No enumerations: `move`, `replay`, `trace`, `scope`, `target`, `platform`, `references` and `action` are free strings whose values are listed in prose.
- `tools/list` (149 to 154) sends no `annotations`. A client cannot tell the 29 read-only tools from `delete_results`, so it must ask the person for each or allow all.
- The 53 declarations are 28 KB, about 7,000 tokens in every session that loads them. Three descriptions are over 500 characters (`why` 622, `explore_app`, `update_scraper_config`); with their parameters the longest declarations pass 1,100.

### 10. Side effects: mostly said, four are not

- `why` runs a build and a scenario (finding 2).
- `verify_page_objects` also writes its outcome into the project (`ScraperService.Workflow.cs:262`); the description names only the browser.
- `set_state` and `step_start` leave a runner paused. `step_start` says "no run_scenario meanwhile"; `set_state` does not, and neither `run_scenario` nor `why` says a Step-through blocks it.
- `write_code`, `draft_test`, `draft_page_objects` and `review_page` write files with no preview call, while smaller writes (`update_scraper_config`) ask first. `write_code` says "Show the user the code first" and cannot check it.

### 11. The instructions name 19 tools and miss the split

The instructions (144) are one 1,470 character paragraph.

- They never say there are two families, or that `list_suites` and `list_scraped_projects` are where each starts.
- For page objects they name `list_recorded_pages`, `get_page_snapshot` and `find_similar_elements`: the hand-made path. `get_scraper_progress` (which gives the next thing to do), `draft_page_objects`, `detect_patterns`, `control_brief` and `verify_page_objects` are not named.
- `tests_affected`, `coverage`, `http_calls`, `rename_step` and `validate_scenario`'s partner `find_step_usages` are not named.
- Half the text describes `run_scenario`'s and `why`'s results, which their own descriptions repeat.

### 12. One suite that cannot load fails every suite tool

On this checkout `list_suites`, `list_steps` and `failing` all answered `… failed: TypeLoadException: Could not load type 'Bruinet.Html.Pages.PageObjectBase`1'`.
`list_suites` has `Built` and `Reason` for exactly this (1431), but `TryGetCatalogs` catches only `BddWorkspaceException` (`BddSuite.cs:307`), so the error reaches the catch-all (241).
`failing` with no `suite` walks every suite (805), so one stale build hides every other suite's failures. The message names no suite and no fix (build it).

## Proposals

Each is a row for `ten-agent-proposals.md` to take or leave. None changes what a tool does unless it says so.

| # | Proposal | Findings |
| --- | --- | --- |
| M1 | Prefix the Scraper's tools with `scrape_`, and say the split in the instructions. | 1, 11 |
| M2 | One results tool; `why` stops running. | 2 |
| M3 | One cap rule: a `max` parameter and a `more` count on every list. | 3 |
| M4 | Skip nulls in every answer. | 4 |
| M5 | Paths relative to the solution, lines one-based, everywhere. | 5 |
| M6 | Every `line` through `ScenarioLine`. | 6 |
| M7 | One name per parameter. | 7 |
| M8 | One verb per act. | 8 |
| M9 | `required`, `enum` and `annotations` in the schema. | 9, 10 |
| M10 | Rewrite the instructions as two short lists. | 11 |
| M11 | A suite that cannot load is a row, not an error. | 12 |

### M1. The renames

| Today | Proposed | Why |
| --- | --- | --- |
| `run_test` | `scrape_run_drafts` | Ends the clash with `run_scenario`; it runs drafts, not any test |
| `draft_test`, `test_brief` | `scrape_draft_test`, `scrape_gap_brief` | The brief is of a gap |
| `list_recorded_pages`, `get_page_snapshot`, `diff_page`, `capture_page` | `scrape_list_pages`, `scrape_get_page`, `scrape_diff_page`, `scrape_capture_page` | "page" on this side is always a recorded page |
| `list_controls` | `scrape_list_page_objects` | It lists page objects first; ends the clash with `list_pages` |
| `list_recorded_actions`, `resolve_actions` | `scrape_list_events`, `scrape_resolve_events` | The result's rows are events (captures and actions) |
| `search_recordings`, `find_similar_elements` | `scrape_find_text`, `scrape_find_similar` | One verb for finding |
| `check_code`, `write_code` | `scrape_build`, `scrape_write_file` | Says the project is the Scraper's and that it builds |
| `start_recording`, `stop_recording` | `scrape_start`, `scrape_stop` | Leaves "record" to `record_video` |
| `review_page` | `write_review` | It writes a file; ends the third meaning of "page" |
| `list_scraped_projects`, `get_scraper_progress` | `scrape_list_projects`, `scrape_next` | `scrape_next` is the entry point: it answers what to do |

The other Scraper tools take the prefix and keep their name (`scrape_detect_patterns`, `scrape_control_brief`, `scrape_match_controls`, `scrape_suggest_controls`, `scrape_header`, `scrape_explore`, `scrape_draft_page_objects`, `scrape_verify_page_objects`, `scrape_update_config`, `scrape_create_project`, `scrape_rename_page_object`).
Rename `serverInfo.name` to `bruinet`.

### M2. The failure tools

- Merge `last_runs` into `failing` as `list_results`: `suite` or `path`, and `status` (`failed` when left out, `all` for what `last_runs` gave). One row shape: file, line of the heading, scenario, status, when, kind, 300 characters of message.
- Add the heading's line to each row, so `get_trace` can be called from it without opening the file.
- `why` becomes `diff_since_green` and only reads. With no traced failure it answers "run it with run_scenario (build: true) first", as `get_trace` already does (1037).
- `run_scenario`'s note (856) and `failing`'s description name the same next tool: `get_trace` for what the run did, `diff_since_green` for what changed.
- `http_calls` becomes `get_http_calls`, beside `get_trace`.

### M3. One cap rule

- Every tool in finding 3's table takes `max` (50 when left out, 400 lines for the outline) and answers `more: <count>` when it left rows out, null otherwise.
- `get_page_snapshot`'s `maxLines` becomes `max`.
- `MoreFiles` and `CodeChangedMore` become `more` inside their own object; `http_calls`' `More: 0` becomes null.
- `search_recordings` and `diff_page` say `more` instead of stopping silently.
- `list_steps` without `query` on a suite over 200 phrases answers the count and the phrases grouped by page, names only, and asks for a `query` or a `page`.
- `list_pages` takes `page`: without it, page names with a control count; with it, that page's controls.
- `list_recorded_pages` answers each page's newest capture and a capture count; `page` gives one page's captures. Drop `file`: no tool takes it.
- `get_trace` without `step` caps the failed step at its 10 slowest calls and the controls that are shown; `step` still gives it in full.
- `last_runs`' message (in `list_results`) is 300 characters, as `failing`'s.

### M4 to M6

- **M4:** add `DefaultIgnoreCondition = WhenWritingNull` to the options at line 22, and delete the loop at 672.
- **M5:** every path in an answer is relative to the solution with forward or back slashes as the platform has them; `find_step_usages` answers `file` and a one-based `line`; `review_page` resolves `path` and `videos` with `FileUri`'s rule (1286).
- **M6:** `record_video`, `delete_results` and `rename_step` read `line` through `ScenarioLine` (for `rename_step`, only when the file is a feature file). All descriptions say "a line of the scenario".

### M7. One name per parameter

| Thing | One name | Changes |
| --- | --- | --- |
| Go-ahead to write | `confirm` | `rename_step`'s `apply`; `delete_results`' `dryRun: false` becomes `confirm: true` |
| Explore without clicking | `linksOnly` | `explore_app`'s `dryRun` |
| An event's number | `event` | `get_page_snapshot`'s `number`; `diff_page`'s `fromNumber`, `toNumber` become `fromEvent`, `toEvent` |
| Search words | `query` | `search_recordings`' `text` |
| A page object's name | `pageObject` | `set_state`'s `page`; `page` stays the Scraper's page key |
| `suite` left out | The only suite; with several, an error naming them | `Suite()` at 1515 and four descriptions |
| `build` left out | False, and the answer says when the build is older than the sources | `coverage` |
| `headless` left out | True | `explore_app` |
| `timeoutMinutes` | On every tool that runs a process, 10 when left out | `record_video`, `run_test`, `why` if it keeps running |

Name `failed` in `run_scenario`'s `trace` description, or stop accepting it.

### M8. One verb per act

- `list_` for a list, `get_` for one thing, `find_` for a search, `check_` for a check, `draft_` for a generated file, `write_` for the agent's own file.
- So: `validate_scenario` becomes `check_scenario`; `verify_page_objects` becomes `scrape_check_page_objects`; `coverage` becomes `get_coverage`; `tests_affected` becomes `list_affected_tests`; `scraped_header` becomes `scrape_get_header`; `control_brief` becomes `scrape_get_control_brief`.
- The step tools keep `step_` first: they are one session and sort together.

### M9. The schema

- `Schema()` takes a required flag per property and writes `required`; the word leaves the descriptions.
- A property may carry `enum`; the eight in finding 9 get one.
- Each tool carries `annotations`: `readOnlyHint` on the 29 that only read, `destructiveHint` on `delete_results`, `openWorldHint` on `explore_app`, `start_recording` and `verify_page_objects`.
- A test in `McpServerTests.cs` holds every description under 500 characters and every read-only tool to no file write.
- Descriptions over 500 characters lose what M3, M7 and M9 now say in the schema; the confirm tools share one sentence ("Asks first: call again with confirm: true").

### M10. The instructions

Two lists of at most six lines each, one per family, each starting with its entry tool.

- Scenarios: `list_suites`; `list_steps` before a step, `check_scenario` before proposing; `run_scenario`; on red `list_results`, `get_trace`, `diff_since_green`; `step_start` to watch; `record_video` and `write_review` at the end.
- Scraper: `scrape_list_projects`; `scrape_next` says what to do; a recorded test goes resolve, gap brief, draft, run.
- The two rules stay, one line each: never invent a phrase; the person browses and signs in.

### M11. A suite that cannot load

- `TryGetCatalogs` (or `ListSuites` and `Failing` around it) catches `TypeLoadException`, `FileLoadException` and `MissingMethodException` and gives them as the suite's `Reason`: "built against an older Bruinet: build it (run_scenario build: true)".
- `failing` skips such a suite and names it under `notLoaded`.
- The catch-all at 241 keeps its role for real faults.

## Unknown

- **Suite-side sizes.** `list_suites`, `list_steps` and `failing` failed on this checkout (finding 12), so `list_steps`, `list_pages`, `last_runs` and `get_trace` were sized from the code, not from a result.
- **Why the suite did not load.** Likely a test project built before proposal 4 renamed Html's page base; not checked, since this review builds nothing.
- **The larger Scraper results.** `detect_patterns`, `suggest_controls`, `find_similar_elements`, `resolve_actions` and `diff_page` were not called; the sample project has one page object made from a recording and no test recording.
- **Which clients read `annotations`.** Claude Code and VS Code do; whether the owner's other agents do was not checked.
- **Existing callers.** The 13 skills, `AGENTS.md`, the VS Code and Visual Studio extensions and Tomorrow's skills name these tools; a rename needs their rows in the change list (review 7 would find them).

## For the owner

1. **Rename now or never.** My proposal: M1 and M8 together in one commit before the first release, since each later rename breaks every skill that names a tool.
2. **Does `why` keep running the scenario.** My proposal: no (M2); one tool that reads and one that runs is the rule every other pair follows.
3. **Order.** My proposal: M11, M4 and M6 first (small, no rename), then M3, then M1 with M7 to M10 as one pass over the list.
