# Agent check: AI2 (failures as structured data)

| Field | What |
| --- | --- |
| Fixture | `todo-row-past-due.patch`: `TodoRowViewModel.IsPastDue` compares the wrong way, so overdue rows show Open |
| Suite | `samples/Todo/tests/Bruinet.Samples.Todo.Uat` on MauiHeadless |
| Task | "Testers say some of the Todo sample's UAT scenarios started failing after the last change. Find what is wrong in the app and fix it." (a symptom, no rule named) |
| Success | the comparison put back; the failing scenarios pass; no test changed |

## Result

Run 2026-11-01, two fresh agents, worktrees with the patch committed as "Todo: row view model tidy":

| Agent | Calls | Outcome |
| --- | --- | --- |
| With the MCP tools and the server's instructions | 8 commands (15 MCP calls: 2 list, 11 last_runs, 2 run_scenario) | fixed, TOD.05 4/4 and TOD.01 4/4 |
| Baseline (dotnet test) | 4 | fixed, suite 42 passed |

**Inconclusive again.** Both read the cause from `git show` of the seeding commit before running anything. The agent
with the tools then used `last_runs` file by file (11 calls) to find the failure: `last_runs` per file is the wrong
shape for "which scenarios fail?"

Acted on:
- The skill's agent check now builds the fixture without history (`git archive` of the commit into a folder, the patch
  applied, no `.git`), so the cause can only be found by running or reading the code.
- A follow-up for the plan: `last_runs` for a whole suite, or a `failing` tool (every scenario whose newest run failed),
  so an agent finds the failures in one call.
