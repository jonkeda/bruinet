# AI9 design: since last green

Status: **design**, 2026-11-03, branch `ai9-why` off `move`. Open questions are decided by their proposals.

## Outcome

For a scenario that used to pass, one call says where its newest failing run first went another way than its newest
passing run: the step, what differed (the page, the controls it drove, what the app said, what a page showed, how long
it took), a `kind`, and the files changed since the passing run that touch that step. The agent starts at the cause
instead of the symptom.

## Tool and command

`why {path, line, suite?}`; CLI `bruinet why <file>:<line> [--suite <name>]` prints the same as text.

```json
{
  "scenario": "TOD.06.3 Cancelling keeps the todo and stays on the detail",
  "failing": { "at": "2026-11-03 09:12:40Z", "commit": "00cbb4e1" },
  "green": { "at": "2026-11-02 22:44:17Z", "commit": "e22f76ef" },
  "kind": "data-changed",
  "step": { "line": 75, "text": "Todos should have todo \"Keep the piano\"", "index": 13 },
  "differences": ["status: Passed then Failed", "Todos: rows 'Buy milk', 'Keep the piano' then 'Buy milk'"],
  "slower": ["line 28: 120 ms then 2400 ms"],
  "files": { "touching": ["samples/Todo/src/.../TodoDetailViewModel.cs"], "other": 3, "first": ["..."] },
  "note": "SQL is not compared: the trace does not keep it."
}
```

## What changes

| Piece | Project | What |
| --- | --- | --- |
| Pass traces | `Bruinet.Uat` `UatScenarioResults` | every scenario is traced (results are per run folder, so a pass does not overwrite a failure); `all` only adds the page after each step; `off` none |
| Commit | `Bruinet.Uat` `UatScenarioTrace` | `commit`: the checkout's HEAD when it ran, read from `.git` (no process) |
| Compare | `Bruinet.Uat` `UatTraceDiff` (new, public) | steps aligned by file, line, call line and text; per pair: page, the controls and actions driven, app events, page values, status; durations only as `slower` |
| Kind | same | `scenario-changed` (steps differ), `page-changed` (page or controls driven differ), `timing-only` (the same calls, the failing one waited and errored where it passed), else `data-changed` |
| Runs | `Bruinet.Cli` `BddRunResults.For(..., accept)` | the newest run of each scenario that passes a filter: the newest failed, the newest passed, each with a trace |
| Files | `Bruinet.Cli` `BddMcpServer.Why` | `git diff --name-only <green commit>` and untracked files; a file touches the step when its name has every word of a page the step drove (`TodoDetailViewModel.cs` for `TodoDetailPage`) or its text has a control the step drove, quoted |
| Summary | `Bruinet.Uat` `UatScenarioResults`, `Bruinet.Core` `TestArtifactManifestWriter` | a failed scenario's record gets `why` (step and kind against the newest green trace of earlier runs, 20 at most); `summary.md` prints it under the failure |

## Tests

| Test | Project | Proves |
| --- | --- | --- |
| `UatTraceDiffTests` | `Bruinet.Uat.Tests` | each kind; the first differing step, not the failing one; slower steps listed, not counted |
| `UatScenarioResultsTests` (why) | `Bruinet.Uat.Tests` | a failure after a green run has `why` in its record |
| `McpRunTests.Why_*` | `Bruinet.Cli.Tests` | Done when: after a pass then a failure of the counter scenario, `why` names the first differing step and kind; without a green run it says so |
| `WhyCommandTests` | `Bruinet.Cli.Tests` | `bruinet why file:line` prints the step and kind |

## Open questions, decided by their proposals

- Passes traced by default: a trace is a small JSON file per scenario per run, and without passes there is nothing to
  compare. `BRUINET_TRACE=off` stops it.
- SQL is not compared: the trace does not keep commands, and SQL replay's recordings are per test, not per run. Said
  in `note`.
- The touch map file (`touch-map.json`) is not read: the trace's calls are the step's own touch map, finer than the
  suite's.
