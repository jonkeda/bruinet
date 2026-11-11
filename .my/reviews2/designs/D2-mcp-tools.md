# D2. The MCP tools

Design for R2-T94, R2-T95, R2-T96 (review 03, M1–M11). The detail per proposal is in `03-mcp-tools.md` § Proposals; this file decides the shape, the order and who must change with it.

## Decisions asked

1. **Rename now, all at once, no aliases.** My proposal: yes, in one commit before the first `Shipped` line; an old name answers nothing. Why: every later rename breaks every skill again, and an alias doubles a list that is already too long for an agent to choose from.
2. **`why` stops running.** My proposal: yes; it becomes `diff_since_green` and reads only. One tool reads, one runs, as every other pair.
3. **Prefix, not a second server.** My proposal: one server, the Scraper's tools prefixed `scrape_`. A second server would need its own `.mcp.json` entry in every consumer and its own host.

## Shape

| Family | Entry tool | Rule |
| --- | --- | --- |
| Scenarios (24) | `list_suites` | Verbs `list_`, `get_`, `find_`, `check_`, `run_`, `write_`, `step_…` |
| Scraper (29) | `scrape_next` (was `get_scraper_progress`) | Same verbs after `scrape_` |

- Names: review 03 § M1 and § M8 tables, as written. Additions: `failing` + `last_runs` → `list_results` (M2), `http_calls` → `get_http_calls`, `review_page` → `write_review`.
- Every list: `max` (default 50) and `more` (count left out, null when none). M3's per-tool rules stand.
- Every answer: nulls left out (M4); paths relative to the solution, lines one-based (M5); `line` is any line of the scenario, read through `ScenarioLine` (M6).
- Parameters: M7's table. Go-ahead is always `confirm: true`.
- Schema: `required`, `enum`, and `annotations` (`readOnlyHint` on the 29 readers, `destructiveHint` on `delete_results`, `openWorldHint` on the three that open a browser). Descriptions under 500 characters (M9).
- A suite that cannot load is a row with its reason; `list_results` names it under `notLoaded` (M11).
- Instructions: two lists of at most six lines, one per family (M10). `serverInfo.name` becomes `bruinet`.

## Order

1. R2-T94, no renames: M11, M4, M6. Small; consumers see nothing but better answers.
2. R2-T95: M3, caps. Answers change shape (`more`), names do not.
3. R2-T96, one commit: M1, M2, M5, M7–M10, and every Bruinet file that names a tool.

## Tests

- `McpServerTests.cs`: the tool list matches a checked-in list of names (a rename is then a visible diff); every description under 500 characters; every `readOnlyHint` tool writes no file (run against a temp solution); every list honours `max` and `more`.
- A suite built against an older Bruinet gives a row, not an error (M11), with a fixture that throws `TypeLoadException`.

## Who changes with R2-T96

| Where | Files |
| --- | --- |
| Bruinet | `BddMcpServer.cs` and partials, `ScraperService` entry points, `AGENTS.md`, `.github/skills/narrated-review`, `.github/skills/scrape-pages`, `.github/agents/bruinet-scraper.agent.md`, two prompts, `.claude/skills/narrated-review`, `docs/guides/` (scraper, narrated-recordings, http-replay, uat-template-guide, visual-studio-preview), AD-024, `changes.md` (a Made row per rename) |
| tomorrow (phase 9) | `.claude/skills/step-show/SKILL.md`, `.sdlc/sdlc.md`; the closed `.sdlc` records stay as written |
| construction (phase 9) | `AGENTS.md:17`, `.github/instructions/ui-tests.instructions.md`, `.github/skills/module-videos/SKILL.md`, `.docs/tests/020-Writing-An-E2E-Test.md`; `.migration` archives stay |
| kofflard | Nothing names a tool |

The IDE extensions do not call MCP tools (they speak LSP); checked by search in `tools/Bruinet.Bdd.VsCode` and `tools/Bruinet.Bdd.VisualStudio` at R2-T96.
