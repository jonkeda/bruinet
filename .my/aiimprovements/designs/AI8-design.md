# AI8 design: a per-step trace, text first

Status: **design**, 2026-11-02, branch `ai8-trace` off `move`. Open questions are decided by their proposals.

## Outcome

After a failed run an agent reads, step by step, what the run did: the page it was on, each control call with its
duration and attempts (a wait shows as a long call or many attempts), what the app said, and the page's controls after
the failed step. It reads it in one call, without running again. A pass is traced too when asked, for AI9's diff.

## Tool

`get_trace {path, line, step?, suite?}`: the newest trace of the scenario at `line`.

Without `step`: one row per step, and the failed step in full.

```json
{
  "scenario": "TOD.06.1 Delete asks for confirmation", "status": "failed",
  "steps": [
    { "line": 24, "text": "Given the app shows the list", "status": "Passed", "ms": 410, "page": "Todo List",
      "calls": 2, "slowest": "Todo List.Add.Tap 180ms" },
    { "line": 27, "text": "When I tap Delete", "status": "Passed", "ms": 95, "page": "Todo Detail", "calls": 1 },
    { "line": 28, "text": "Then the dialog should ask \"Delete todo?\"", "status": "Failed", "ms": 5012, "page": "Todo Edit",
      "calls": 1, "slowest": "Todo Edit.Dialog.Exists 5004ms x24", "events": 0 }
  ],
  "failed": { "line": 28, "calls": [ { "page": "Todo Edit", "control": "Dialog", "action": "Exists", "ms": 5004, "attempts": 24, "result": "error" } ],
              "events": [], "pageAfter": { "page": "Todo Edit", "controls": [ { "name": "Title", "kind": "Entry", "shown": true, "value": "Buy milk" } ] } }
}
```

With `step` (a step's line): that step in full, as `failed` above.

## Collection

| Piece | Project | Class | What |
| --- | --- | --- | --- |
| Calls | `Bruinet.Core` | `Logging/StepTrace` (new) | every control call (`ControlCall` through `Timing.Call`): page, control, action, start, ms, attempts, result, under the step's `TestScope`; a ring of 2000; `BRUINET_TRACE=off` keeps none |
| Step's calls | `Bruinet.Bdd` | `BddStepResult.Calls` | `StepTrace.Of(scope)`, read like `AppEvents` |
| Page after | `Bruinet.Bdd` | `BddExecutionContext.PageReader`, `BddStepResult.PageAfter` | read after the step, outside its scope (its reads are not its calls): after a failed step always, after every step with `BRUINET_TRACE=all` |
| Reader | `Bruinet.Uat` | `UatRuntime.ConfigureContext` | `PageReader = ReadCurrentPage` (`UatPageState`, 200 ms per control) |
| File | `Bruinet.Uat` | `UatScenarioTrace` (new), `UatScenarioResults.Record` | `<OutputDirectory>/traces/<feature>__<scenario>__trace.json` for a failed scenario, for every one with `BRUINET_TRACE=all`; the report names it (`tracePath`) |
| Read | `Bruinet.Bdd.Workspace` | `BddScenarioRun.TracePath` | from the report |
| Tool | `Bruinet.Cli` | `BddMcpServer.GetTrace` | the summary and a step |

Caps: 50 calls a step (the slowest kept when more), the AI6 grouping for events (10 a step), 60 controls a page,
values at 200 characters (as `step_state`).

## Tests

| Test | Project | Proves |
| --- | --- | --- |
| `StepTraceTests` | `Bruinet.Core.Tests` | calls under a step's scope are its; another step's are not; off keeps none |
| `BddStepTraceTests` | `Bruinet.Bdd.Tests` | a step's `Calls`; `PageAfter` after a failure, not after a pass; with all, after each |
| `UatScenarioTraceTests` | `Bruinet.Uat.Tests` | the trace JSON: steps, calls, events, the page, the caps |
| `McpRunTests.GetTrace_*` | `Bruinet.Cli.Tests` | Done when: a failed counter run gives a trace whose failed step has its page and calls; a step's detail; no trace for a pass by default |

## Open questions, decided by their proposals

- Page before a step: not read; the step before's `pageAfter` is it, with `all`. Reading before every step doubles the
  cost for what the previous row already shows.
- On a failure without `all`, only the failed step's page is read: its cause is usually on that page, and reading each
  page costs up to 200 ms a control.
- One trace per scenario, the newest, overwritten as the report is. AI9 keeps the last green one when it needs it.
