# Morning report, 2026-11-02

The night's run: AI1, AI2, AI2c, AI3 and AI5 done; AI2b and AI4 partly; C4 and C5 built with their coverage targets
open. Thirteen commits on `move`, pushed in four batches (origin and the jonkeda mirror). Each item was reviewed by a
separate agent and its findings fixed or recorded (`AI1-review.md`, `AI2-review.md`, `AI2b-review.md`, `AI4-review.md`).

## What an agent can do now (the MCP server)

- Run a scenario and wait for it (`run_scenario`), with the failure in the answer: kind, expected and actual, locator,
  binding and the code frames to look in; `get_failure` and `failing` find failures afterwards, `last_runs` the results.
- Step through a scenario (`step_start`, `step_move`, `step_screenshot`).
- See which app lines one scenario runs (`coverage`), the tests a change reaches (`tests_affected`), and rename a step
  or a scraped page object safely (`rename_step`, `rename_page_object`).

## Bugs found on the way

| Where | Bug |
| --- | --- |
| `BddInvoker` | a step's exception lost its own stack (`throw ex.InnerException`): IDE, logs and reports showed only Bruinet's frames |
| Runner host | a Run or Step-through that failed early kept no result |
| `NavMenuControl` (Blazor) | the first link was always "active" (a selector list took the state on its last part only) |
| MAUI sample | the slider started at 1, not 50 (value set before its bounds) |
| Todo sample | its SQL replay looked for recordings from the process folder, so the IDE's Run failed on it |

## Waiting on you

| # | Question | Proposed |
| --- | --- | --- |
| 1 | C4 (Blazor 58.3%, 75 asked) and C5 (Maui 50.9%, 80 asked) | count targets on hand-written code; generated wrappers and device-only paths apart |
| 2 | C5's contracts never ran on the Windows head | run them once in a desktop session (with C8); until then `--filter "Head!=MauiHeadless"` |
| 3 | The MAUI picker starts with nothing chosen; its binding chooses first | the sample's view model starts with a choice |
| 4 | AI2b is a separate `coverage` tool, not `run_scenario {coverage: true}`; Done-when not run on bUnit or Chromium | keep the tool; run the Done-when with the next browser run |
| 5 | Agent checks proved nothing yet (both solved by reading a comment, then by `git show`) | the skill now builds fixtures without history; keep them, one per item |

## Not done, and why

- Not run: the Windows head (it drives the desktop), the Tomorrow submodule pointer (not moved).
- Eight `Bruinet.Cli.Tests` fail only in the run's separate build folder (`TestResults/ai-build`, used because 19
  running `bruinet` processes hold `tools/Bruinet.Cli/bin`): they read files three folders above their binary.
- `StepTests.RunOfTheFile_...` failed once under load and passed three times alone: a race in the test.

Expected next action: answer question 1, then `/ai-item next` (C6, mutation testing of the controls).
