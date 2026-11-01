# AI2 review: failures as structured data (with AI2c)

Status: **done**, 2026-11-02. Commits 2f0b6db7 (AI2), f37cf0fc (AI2c `failing`), and the review fixes after them.

## Checks

| Check | Result |
| --- | --- |
| `Bruinet.Uat.Tests` | 251 passed |
| `Bruinet.Bdd.Tests`, `Bruinet.Bdd.Workspace.Tests` | 277 passed (2 skipped as before), 71 passed |
| `Bruinet.Cli.Tests`, MCP and step tests | 43 passed, 1 failed once: `StepTests.RunOfTheFile_...` (a Step-through start must be refused while the counter's Run goes; under load the Run ended first), passed 3 of 3 alone: a race in the test, not changed |
| End to end, by hand | the Todo seeded bug (`todo-overdue-done.patch`) through `run_scenario` on MauiHeadless: kind `assertion`, expected "Done", actual "Overdue", frames `TodoStatus.AssertStatus (TodoStatus.gen.cs:48)`, `TodoList.AssertRowStatus (TodoList.cs:83)` |
| Agent check | inconclusive, see `checks/AI2.md`; it found that agents need `failing` (AI2c, built) |

## Review findings and what was done

| Finding | Done |
| --- | --- |
| Blocker: the Done-when asked for expected 5 / actual 1 from the counter suite, which uses `Assert.True` | the design's Done-when corrected to what is measurable; the Todo check above recorded |
| A fault while describing the failure dropped the whole report | `UatStepFailure.Of` guards itself: `{kind: error, unreadable}` instead |
| Framework assemblies hand-listed and incomplete | every `Bruinet.*` assembly is framework except samples (`.Samples.`) and suites (`*Tests`, `*.Uat`); bUnit, AngleSharp, FlaUI, WebDriver, Appium, MauiHeadless, NUnit, MSTest added; `System` matched exactly or as `System.` |
| `app-exception` decided on frame 0, so a runtime throw from app code (`int.Parse`) was `error` | the first frame outside the runtime decides; test |
| Async and lambda frames named by the compiler | named by the method they were written in; test |
| `expected`, `actual`, `condition` uncapped | capped at 500 characters |
| A step's `durationMs` counted breakpoints and pacing | the clock starts at the handler |
| A code comment pointed into `.my/`; internal XML docs | pointer removed; docs trimmed |
| `uat-template-guide.md` did not describe the new fields | its Reporting section does |
| Missing tests (old report parses, new fields read, more kinds, locator from `ElementNotFoundException`) | added |
| `get_failure`'s file relative to the config, no step text | relative to the solution, with the step's text |
| `"failure": null` on every passed step | left out when there is none |
| `binding` only the command id | recorded as a narrowing in the design: file and line come through `frames` |
| Steps that fail without an exception (a `false` return, a page not ready) are `error` | recorded, not done: they need the result's source, for a later item |
| `ScenarioNames` repeats the Run's lookup | left: two short uses, different callers |
