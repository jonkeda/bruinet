# AI8 agent check

| Field | What |
| --- | --- |
| Fixture | `todo-delete-before-asking.patch`: `TodoDetailViewModel.DeleteAsync` starts the delete before the confirmation, so Cancel still deletes; TOD.06.3 fails at its last step, "Todos should have todo "Keep the piano"", nine steps after the cause |
| Task | "TOD.06.3 Cancelling keeps the todo and stays on the detail fails in Bruinet.Samples.Todo.Uat; fix the app, not the tests, and confirm TOD.06 passes" |
| Success | the delete awaited only after the confirmation; TOD.06.1-3 pass |
| Baseline | `get_trace` hidden, `BRUINET_TRACE=off`: 2 bridge runs, 2 tool calls (`run_scenario` twice), 4 file reads and searches; fixed |
| Result | 2 bridge runs, 2 tool calls (`run_scenario` twice), 2 file reads and searches; fixed. Passed by the rule (no more calls), but `get_trace` was not used |

Run on 2026-11-02 by two fresh general-purpose agents, one after the other, on a `git archive` copy of `ai8-trace`
with the patch applied (no `.git`, no `.my`), headless, through `mcp-bridge.py`.

What it showed:
- As in AI7's check, the bug was one grep away: "delete" in the Todo app's sources finds `DeleteAsync`, and the early
  `var deleting` reads as wrong. The Todo sample is too small for a tool that shows what a run did to beat reading
  its code.
- A check that needs the trace needs a cause the source does not show: a timing or environment fault (a slow
  backend, a stub's answer, data a test set brings), or an app too large to read around the failing step. Proposed for
  AI9's check, which needs passing and failing traces anyway.
