# AI2 design: failures as structured data

Status: **built**, 2026-11-01 (2f0b6db7, review fixes after). No open question needs the owner (decisions below, recorded per the run's rules).

## Where the data comes from

| Field | Source |
| --- | --- |
| kind | the failed step's exception type (table below) |
| expected, actual, locator | `AssertionException.Expected/Actual/ControlLocator`, `ElementNotFoundException.LocatorString`, `ElementNotReadyException.Locator/Reason/Detail`, `ScopeNotReadyException.Readiness`, `WaitTimeoutException.TimeoutMs/Condition` |
| binding | the step's `BddStepInvocation.Command` (`CommandId`: type and method), its file and line from `BddSourceLocator` |
| frames | the exception's stack: the first 5 frames outside `System.`, `Microsoft.`, `Xunit`, `Bruinet.Core`, `Bruinet.Bdd`, `Bruinet.Uat` (user code: page objects, steps, the app in process) |
| step duration | timed in `BddStepExecutionSession.RunNextAsync`, new `BddStepResult.Duration` |
| screenshot | the result's `evidencePath` (already written) |

| Exception | kind |
| --- | --- |
| `AssertionException`, xUnit's `XunitException` | `assertion` |
| `ElementNotFoundException`, `StaleElementException` | `locator-not-found` |
| `ElementNotReadyException`, `ScopeNotReadyException`, `WaitTimeoutException`, `TimeoutException` | `timeout` |
| `AppUnavailableException`, `PageLoadException`, `RouteUnavailableException` | `app-unavailable` |
| a step that binds to nothing | `bind` |
| anything thrown from user code frames only | `app-exception` |
| otherwise | `error` |

## Changes

| Project | Change |
| --- | --- |
| `Bruinet.Bdd` | `BddStepResult` gains `Duration` (optional, `with` in `RunNextAsync`); PublicAPI.Unshipped |
| `Bruinet.Uat` | `UatStepFailure` (internal): built from a failed `BddStepResult`; `UatScenarioReport.Build` writes `durationMs` per step and `failure` on the failed step |
| `Bruinet.Bdd.Workspace` | `BddScenarioRun.Parse` reads `durationMs` and `failure` (both optional: older results parse as before) |
| `tools/Bruinet.Cli` | `get_failure {path, line}` from the newest result of that scenario (`RunsOf`); `run_scenario` adds `failure` for a failed scenario from the result the run just wrote |

## Tool result

```json
{ "scenario": "TOD.05.3 Overdue ...", "address": "Features/...#TOD.05.3-...", "status": "failed",
  "failedStep": { "file": "...", "line": 46, "text": "...", "durationMs": 812 },
  "kind": "assertion", "expected": "Done", "actual": "Overdue", "locator": "AutomationId:StatusText",
  "binding": { "method": "TodoListSteps.AssertRowStatus", "file": "...", "line": 88 },
  "frames": ["TodoListPage.Todos ... line 40"], "screenshot": "...", "message": "... (capped 2000)" }
```

## Decisions

- Recorded in `__result.json`, not passed over the runner pipe: `get_failure` then works for every run (the gate, the
  IDE, run_scenario), and the pipe stays as it is.
- The page object member (`member` in the plan) is left to AI7 (element to source): the locator is given now.
- Times as a per-step `durationMs`; the start time (bdd-gaps D1) waits for D1, which is parked.

## Tests

| Test | Project |
| --- | --- |
| each kind from a thrown exception (`UatStepFailureTests`) | `Bruinet.Uat.Tests` |
| a failed step's `failure` and every step's `durationMs` in the report | `Bruinet.Uat.Tests` |
| an old result without them parses | `Bruinet.Bdd.Workspace.Tests` |
| `get_failure` after a failed run; under 500 tokens; error when no failed run | `Bruinet.Cli.Tests` (`McpRunTests`) |

Done when (as built; the first wording asked for what the counter suite cannot give): the counter suite's failure is
kind `assertion` with its binding and the fixture's frame (`BddRunnerProcessTests`, `McpRunTests`), and the Todo seeded
bug through `run_scenario` on MauiHeadless gives expected "Done", actual "Overdue" and the page object's frame
(`TodoList.AssertRowStatus (TodoList.cs:83)`): run by hand on 2026-11-01, see AI2-review.md. The counter step uses
xUnit's `Assert.True`, which carries no expected or actual; xUnit's own exceptions are not read for them.

Narrowed: `binding` is the command id; its file and line come through `frames` when the step's code is on the stack.
