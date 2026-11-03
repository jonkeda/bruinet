# AI9 review: since last green

## What was built

- `why {path, line}` and `bruinet why <file>:<line>`: the newest failing run's trace against the last green one. They
  give the first step where the runs part, a kind (`page-changed`, `data-changed`, `timing-only`, `scenario-changed`),
  steps that got much slower, and the changed files. The files are split into those touching that step, those touching
  an earlier step, and how many others.
- A failed scenario's line in `summary.md` gives that step and kind.
- Every scenario that passes or fails is traced, with the commit it ran on. A pass's trace is its green file, which a
  later failure in the same run folder (a warm runner keeps one) does not overwrite.
- On the seeded Todo bug (delete before asking), `why` said `data-changed` at the failing step, line 75. The cause file,
  `TodoDetailViewModel.cs`, came first among the files touching an earlier step. Nothing between drives the deleted
  row, so the step where the runs part is the symptom's.

## Checks

- Core 99, Uat 264, Workspace 91: passed.
- Cli: 394 passed; the 8 generator and data validator tests fail under `--artifacts-path`, as before.
- The Todo UAT suites, before the review fixes: MAUI headless 42 passed, 1 skipped; web on bUnit 32 passed, 2 skipped,
  and TOD.08.8 failed (the known bUnit flake).
- Agent check (checks/AI9.md): passed in the fifth round, 2 tool calls as the baseline's, 2 file reads against 3. The
  rounds before it changed `why`: comment-only changes set apart, `sinceGreen` in failures, `why` builds and runs the
  scenario when no failure is traced.

## Review

The independent review found two blockers. Both are fixed:

- Steps were matched by line, so an edit above them gave `scenario-changed`. Now they are matched by file, text and
  whether they are a callable's.
- `timing-only` matched Bruinet's own value-wait messages ("within 5000 ms"). Now it needs the failed call to have
  taken at least half as long in the green run.

Its concerns, and what was done:

- Fixed: `LastGreen` always looks in the failing run's own folder, and orders the others by their manifest's time.
- Fixed: the note says when page values were not compared (the green run did not read pages).
- Fixed: pages are compared from the same source on both sides; a step with no known page is not a change.
- Fixed: git runs in the suite's repository; it diffs the green commit against the failing one when HEAD has moved since.
- Fixed: 2000 files at most, each read once, binaries skipped, 20 named in each list.
- Fixed: the commit must be hex; `core.quotePath=false`.
- Fixed: the note says when a newer run passed.
- Fixed: line breaks are removed from the summary line.
- Fixed: a pass writes only its green file.
- Added: tests for the summary line, `GitHead` (worktree, packed refs, detached, reftable) and the file sorting.
- Fixed: comments still described AI8's policy.
- Fixed: the types are internal (`InternalsVisibleTo bruinet`); Core's key is a neutral `detail`; `WhyCommand` braces
  and drive-letter parsing.
- Left: git has no timeout (`SqlCommands.GitText` is shared with `bruinet sql`).
- Left: the test flag is in the user's temp folder (the counter fixture does not know its solution).
