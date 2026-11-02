# AI8 review: a per-step trace, text first

## What was built

- A failed scenario writes `step-traces/<feature>__<scenario>__trace.json` beside its report: per step, its control
  calls (duration, attempts), app events, and the page's controls after the failed step.
- `BRUINET_TRACE=all` (`run_scenario`'s `trace: all`) traces passes too and reads the page after every step; `off`
  traces nothing.
- `get_trace {path, line, step?}`: a row per step (page, calls, the slowest call, events) and the failed step in full,
  or one step in full.
- On the seeded Todo bug (Delete bound to Edit), the trace showed the dialog step waiting 5 s over 161 attempts, and
  the detail page's controls all hidden: the app had left the page.

## Checks

- Core 98, Bdd 287, Uat 253, Workspace 91: passed. One exception, Core's `ScreenshotServiceTests.AContextWithNoPixels`:
  it fails on `move` too, when the whole project runs.
- Cli: the MCP and runner tests passed (49); the 8 generator and data validator tests fail under `--artifacts-path`, as
  before.
- The Todo UAT suites: MAUI headless 42 passed, 1 skipped; web on bUnit 33 passed, 2 skipped, and after the fixes 32 with
  TOD.08.8 failing (the known bUnit flake, about 1 run in 4, on the base too).
- Agent check: not run yet.

## Review

The independent review found three blockers. All are fixed:

- A trace error lost the report too: the trace now has its own try.
- Reading the page had no limit: now 60 controls and 3 seconds at most, and the reason says what was left out.
- The tests did not prove the caps, `all` or `off`: added. The mode can be set in tests.

Its concerns, and what was done:

- Fixed: calls are kept per step, not in a shared ring that other tests could fill; calls outside a step are not kept.
- Fixed: nothing is allocated when off or outside a step.
- Fixed: `step` matches a callable's call line, then the failed step of it.
- Fixed: of an outline's rows, the newest with a trace.
- Fixed: an unreadable trace is a clear error.
- Fixed: `run_scenario` takes `trace`, since a variable can't be set from MCP otherwise.
- Fixed: empty fields are left out of the file.
- Fixed: the call count adds the calls left out.
- Fixed: the step's page is the page after it when read.
- Fixed: the folder is `step-traces/`, not a second `traces/`.
- Fixed: `StepTrace.Add` is internal.
- Fixed: an older trace is removed when a run writes none.
- Recorded: no page before a step, in the plan.
