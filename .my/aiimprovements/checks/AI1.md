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

(filled in by the run)
