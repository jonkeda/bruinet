# AI9 agent check

| Field | What |
| --- | --- |
| Fixture | `todo-confirm-buttons-swapped.patch` on a copy with one clean commit and a green run of TOD.06 on it: `ShellDialogs.ConfirmAsync` passes its buttons in the other order (Cancel answers like Delete), and four other files change their comments only (`TodoDetailViewModel.cs` among them, a name that looks relevant) |
| Task | "TOD.06.3 passed earlier today and fails now, after some changes to the app; fix the app, not the tests, and confirm TOD.06 passes" |
| Success | `PlatformServices.cs:23` with `accept, cancel`; TOD.06.1-3 pass |
| Baseline | `why` hidden, `sinceGreen` stripped: 2 bridge runs, 2 tool calls (`run_scenario` twice), 3 file reads and searches; fixed |
| Result | after three changes, 2 bridge runs, 2 tool calls (`why`, `run_scenario`), 2 file reads and searches; fixed. Passed |

Run on 2026-11-03 by fresh general-purpose agents on a `git archive` copy of `ai9-why` (no `.my`) with one commit of its
own, so `why` has a green commit to diff against; the patch on top, uncommitted; headless, through `mcp-bridge.py`.
Neither agent could run git.

The rounds with `why`:

| Round | Calls | Reads | What it showed | Changed |
| --- | --- | --- | --- | --- |
| 1 | 5 | 5 | `why` named `TodoDetailViewModel.cs` (a comment) as touching the step; the cause was among the others | comment-only changes set apart (`commentsOnly`) |
| 2 | 4 | 2 | `why` named `PlatformServices.cs` as the only code change; but `why` before any run was an error, and the agent ran, then asked | `sinceGreen` in `run_scenario`'s and `get_failure`'s failures |
| 3 | 4 | 2 | the agent still asked `why` first | `why` runs the scenario when no failure is traced |
| 4 | 4 | 2 | `why`'s own run used the last build, from before the change, and passed | `why` builds first, as `run_scenario`'s `build: true` |
| 5 | 2 | 2 | `why` built, ran, and named `PlatformServices.cs` as the one code change; one edit, one confirming run | |

What it showed: a tool that diffs against the last green run helps once the change list is short and honest. Comment-only
edits must not count, and the tool must answer in the agent's first call.
