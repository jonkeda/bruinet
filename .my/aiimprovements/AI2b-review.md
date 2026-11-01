# AI2b and C5 review

Status: **partly done**, 2026-11-02. Commits 714608f9 (C5), c25cc7ee (AI2b), and the review fixes after them.

## What AI2b is now

A `coverage` tool: one scenario through the xUnit gate by its address with .NET's own collector (Microsoft.NET.Test.Sdk
brings it; nothing installed), the lines it ran and missed per file. Proved by hand on MauiHeadless: TOD.05.3 runs
`TodoRowViewModel` whole and misses `TodoStatusRules.Next`, in 21 s.

Not built, against the plan: `run_scenario {coverage: true}`, a stored `__coverage.json`, the Done-when on bUnit and
Chromium. Proposal: keep the separate tool (a coverage run is slower and should not slow every run), and run the
Done-when on the Todo web suite with `head: bunit` and `head: web` next time a browser run is due.

## Review findings and what was done

| Finding | Done |
| --- | --- |
| No check that the suite runs by address: a `[Theory]` suite ran nothing and reported "passed" | bound and `[UatScenarioTheory]` checked first; "no test ran" is a tool error |
| No timeout: a hung UI run blocked the server | `timeoutMinutes` (10), the process tree killed |
| A build through `coverage` left the workspace stale | `BuildDone()` after a build |
| The file list came after the slow run; no git failed raw | resolved first; no git asks for `files` |
| Deleted files, XAML and Razor pages reported as "not loaded" | only existing `.cs` and `.razor`; the note says "not in the report" |
| Result size unbounded; the output tail capped from its start | 20 files, each range string capped; the tail kept from its end |
| Temp folder kept on errors; an empty catch | deleted in `finally` by `TryDelete`, which traces what it could not delete |
| Several `<source>` roots mapped to the first | each root tried; test |
| "shows the branch a passing one never took" overstated | "lines, not branches" |
| No head choice | `head` sets `BRUINET_HEAD` for the run |
| Coverage runs' durations land in `last_runs` | recorded, not done |
| C5: the picker binding chooses its own start state | recorded as an open question with a proposal in the coverage plan |
| C5: contracts not run on Windows or Android | the comment says so; `Head=MauiHeadless` trait to filter them; open question in the coverage plan |
| C5: `Slider_SetValue_RespectsBounds_Min` passed without testing | asserts the value |
| C5 and C4 closed under their targets as "decided" | reopened as an open question with a proposal |
| Comments and CHANGELOG naming `.my` plan ids | removed |
