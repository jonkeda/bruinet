# Agent check: AI1 (run_scenario, last_runs)

| Field | What |
| --- | --- |
| Fixture | `todo-overdue-done.patch`: a past-due todo that is Done shows Overdue (`TodoStatusRules.Display`) |
| Suite | `samples/Todo/tests/Bruinet.Samples.Todo.Uat` on MauiHeadless (`BRUINET_HEAD=headless`) |
| Task | "A tester says a finished todo whose due date has passed shows as overdue in the Todo app (samples/Todo). Find the cause with the Todo UAT suite and fix the app. Done when the suite's status scenarios pass." |
| Success | `TOD.05.3` passes; the fix is in `TodoStatusRules.cs`; no test or feature file changed |
| Baseline | without `run_scenario`: the agent's only way to run a scenario is `dotnet test` with a filter it must work out |
| Result | see below |

## Result

Run 2026-11-01, two fresh agents, the same task, worktrees with the patch:

| Agent | Tools | Calls | Outcome |
| --- | --- | --- | --- |
| With AI1 | all, run_scenario and last_runs included | 7 | fixed (the right line), TOD.05 4/4 passed |
| Baseline | run_scenario and last_runs blocked | 9 | fixed (the right line), TOD.05 4/4 passed |

**Inconclusive.** Neither agent ran a scenario before fixing: the method's doc comment ("Overdue when ... the todo is not
done") gave the bug away on reading. The agent with AI1 listed the tools, then still ran `dotnet test` with a filter it had to
find (one miss), never run_scenario. Two lessons, both acted on:

- A fixture must not be solvable by reading: the next one (`checks/`, for AI2) puts the bug where a run is the only way to
  see it (a binding or a page object, no comment naming the rule), and its task names a symptom, not the rule.
- Discoverability: the MCP server's instructions now say to run scenarios with run_scenario; the agent here read only the
  tool list. The skill's agent check gives the agent the server's instructions too, as an MCP client would.

AI1 itself was proven by its contract tests and by the end-to-end run on MauiHeadless (TOD.05 4/4 through run_scenario;
the seeded bug reported at line 46 with "Expected Text to be 'Done' ... Actual: 'Overdue'").
