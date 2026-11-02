# AI6 review: what the app said during a step

Status: **done**, 2026-11-02. Commit 29728c45 and the review fixes after it.

## Checks

| Check | Result |
| --- | --- |
| `Bruinet.Core.Tests`, `Bruinet.Logging.Tests` (new), `Bruinet.Uat.Tests` | 94, 6, 251 passed |
| `Bruinet.Cli.Tests`, MCP and step tests | 50 passed, `step_log` over two runs of one scenario shows the newest run's only |
| MAUI UI suite, Todo UAT suite, MauiHeadless | 388, 42 passed |
| Chromium contracts (the Playwright change) | 97 passed |
| End to end | TOD.08.1 on MauiHeadless: "Sync failed: ServerError" from `TodoListViewModel` on line 26, through the gate and run_scenario |

## Review findings and what was done

| Finding | Done |
| --- | --- |
| Blocker: a step's events were looked up by its text, so a re-run or a same-named step shared them | each step takes `StepLog.Mark()` at its handler and keeps `(mark, scope)` in its result (`BddStepResult.LogWindow`); its events are read when the report is written, matched by the very scope: an async continuation it started still counts, an earlier run never does |
| `TestScope.Latest` never went back; parallel tests | it goes back when a scope ends (unless another was entered since); parallel tests' off-flow entries can still land on another test's step: recorded |
| Playwright's events carry the scope its loop started in | its handlers add `elsewhere`, filed under the latest scope |
| The exception hooks caught almost nothing | removed; the docs no longer claim them |
| The provider added before the app's registrations | after them |
| Exception and source text uncut | every text cut at 500 |
| No architecture rule keeps Microsoft.Extensions.Logging out of Core | the design's claim corrected; no rule added |
| The package targeted net10.0 only | the libraries' net8 to net10 |
| The provider tested from Core's tests | `tests/Bruinet.Logging.Tests` |
| No off switch | `BRUINET_STEP_LOG=off` |
| No tests of the report, `step_log` or a re-run | the counter fixture's step that logs; `StepLog_GivesWhatTheAppSaidAtItsStep_OnlyFromTheNewestRun` |
| Results' size: repeats, uncapped fields, no file in `step_log` | a repeated line once with `times`, at most 10 a step, 20 steps, the step's file |
| The sample took the whole logging package | the abstractions, `NullLogger` by default |
| Docs said "console errors" and "exceptions nothing caught" | "errors and warnings", no exception claim; README and structure.md list the package |
| No AD for the new package | recorded, not done |
| The web head's app log | not read: it runs in its own process (a follow-up) |
