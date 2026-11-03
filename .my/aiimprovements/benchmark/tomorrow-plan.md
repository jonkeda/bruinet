# Benchmark round 2: Tomorrow

Status: **plan**, 2026-11-03. Steps run one by one; each ends with a stop for the owner. The method, setups, measures
and rules are round 1's (`plan.md`); this file says only what differs.

## Question

Round 1 found the tools about 8% cheaper on the Todo sample and no more successful (`report.md`). There, reading the
code and running `dotnet test` found every cause. Do the tools help more on an app where the cause is several layers
below the symptom, as in Tomorrow?

## What differs from round 1

| | Round 1 | Round 2 |
| --- | --- | --- |
| App | the Todo sample in Bruinet | Tomorrow's studio (`tests/Tomorrow.Studio.UITests`, 158 scenarios, Blazor) |
| Base | a commit of `move` | a commit of `game` with the submodule it records (step 1: `b1b1d8690`) |
| Head | MAUI headless or bUnit | bUnit (`BRUINET_HEAD=bunit`), the browser only where a task needs a screen |
| Database | none | one per run on `.\MSSQLSERVER01`, already unique per run (`tests/Shared/TestDatabase.cs`) |
| Tasks | T1-T8 | TT1-TT4, all held out: none ever drives a tool change |
| Runs | 48 | 24 in the main round, plus 9 `without-<item>` runs |

The items' agent checks ran on the Todo sample, not on Tomorrow, so step 1 also checks that `run_scenario`, `why` and
the trace tools answer on the studio suite. It declares no states, so AI10's `set_state` is out of this round; its
price is taken on Todo's T4b instead.

## Tasks

Each a seeded fault in code the failing step does not name. A grep of the failure's text finds the page, not the
cause. Causes below are proposals; step 2 fixes each one and checks it.

| Id | Kind | Symptom (failing scenario) | Cause (seeded) | Tools it is for |
| --- | --- | --- | --- | --- |
| TT1 | rule deep below a page, after a tidy-up (green run first) | PLT.35.1 and PLT.35.13: the thugs' brain steps change | `Brain.AttackField`'s search stops at once (`cost >= field[at]`), so closing in is flat; renames in four fight files beside it | `why`, `get_trace`, `coverage` |
| TT2 | rule below a page, no earlier green run | PLT.30.3: the shotgun is not marked out of line | `CompareItems.Marks` takes the dearest better-graded peer, not the cheapest (`OrderByDescending`) | `get_trace`, `coverage`, `where_is` |
| TT3 | regression in a bigger change (green run first) | PLT.30.1: after unticking "green", no item shows, not 57 | `Shelves.Toggle` keeps a facet with no values ticked; renames in five shelf files beside it | `why` (`sinceGreen`) |
| TT4 | look at a screen | (none fails) the Fights bench's chance to hit thug 1, its attack and defence, and thug 1's first brain step's score | none: 45 %, defence 25 and 752.9 are only on the screen | `step_start`, `step_screenshot`, `step_state` |

All four run on bUnit. PLT.30.2 needs a browser and fails there, so `task.json`'s `notOnHead` leaves it out of the
green run and of `expect.json`, and the prompt says so.

## Runs

- The same agent, prompt template and limits as round 1, with a Tomorrow version of `prompt.md` (the studio suite, the
  database server, `BRUINET_HEAD`). Limits: 60 minutes and 40 tool calls.
- Main round: TT1-TT4 × `before`, `all` × 3 = 24 runs, four at a time (disk: about 2 GB a built copy, 20 GB free).
- Then `without-AI9` on TT3 × 3 and `without-AI8` on TT1 × 3, the two items that should matter most here, and
  `without-AI10` on Todo's T4b × 3.
- Cost: Tomorrow's runs read more, about 150k tokens each, so about 3.6M for the main round, 1.2M for the pilot and
  1.2M for the `without` runs.

## Steps

| Step | Output | Done when |
| --- | --- | --- |
| 1 | `make_copy.py` takes a Tomorrow task: `game` and its submodule archived, one commit, built, green run when asked, seeded; the bridge's solution is the copy | one copy of PLT.35 built and run green; its minutes and gigabytes noted; `run_scenario`, `why`, `get_trace` and `step_state` each answer on it |
| 2 | TT1-TT4: seed, prompt, `expect.json`, known fix; `check_task.py` on each | each seeded copy fails as described, its fix scores solved, unfixed scores unsolved |
| 3 | Pilot: TT1-TT4 × `before`, `all` × 1 | the harness runs end to end; fixes to it only |
| 4 | Main round: 24 runs into `tomorrow-results.jsonl` | complete |
| 5 | The `without` runs: 9 runs into the same file | complete |
| 6 | `report.md` gets a "Round 2: Tomorrow" section: solved and cost per setup, which tools were called, what each `without` item cost | the owner reads it |

## Unknown

- Whether a Tomorrow copy builds in a reasonable time. If one takes over 10 minutes, step 1 makes copies from a built
  worktree instead of an archive.
- Whether TT2's cause can hide from grep at all: if the loader's skip leaves a log line naming the row, the task is
  too easy and gets a quieter cause.
- Whether `step_state` reads Blazor pages on bUnit as it reads MAUI ones; TT4 runs on the browser head if not.

## Progress

| Step | State | Commit | Note |
| --- | --- | --- | --- |
| 0 | done | 3d9e0b6a | this plan |
| 1 | done | 314f117c | see "Step 1" below |
| 2 | done | (this commit) | see "Step 2" below |

## Step 1: the harness on Tomorrow

- `make_copy.py`: `"repo": "tomorrow"` archives Tomorrow's commit and the submodule commit it records; `round.py` puts
  such copies in `tomorrow/TestResults/bench`. Inside Bruinet's folder, MSBuild took Bruinet's `Directory.Build.props`
  and the build failed.
- A copy (task `TT0`, PLT.35 on bUnit, no seed): 75 seconds made, built and run green (16 scenarios); 2.3 GB.
- Tomorrow fix (`b1b1d8690` on `game`): the studio suite never ran under Bruinet's runner (`run_scenario`, the step
  tools). Its host looked for `src/Tomorrow.Studio` above `AppContext.BaseDirectory`, which is the runner's copy in the
  temp folder; it now looks above `AssemblyFile.FolderOf`.
- With a hand-made fault in the fight brain: `run_scenario` failed PLT.35.13 and its `sinceGreen` named `Brain.cs`;
  `why` and `get_trace` answered in full.
- `step_state` failed on the studio: today's fallback (the page that says it is loaded) made every page object, and one
  cannot be made from the container. It now reads the page the last step drove, as the trace names it, then the
  loaded page; a page that cannot be made counts as not loaded.

Findings for step 2:

- A single-file fault is given away by `sinceGreen`'s changed files: TT1 and TT2 need noise edits beside the cause, as
  round 1's T7 had.
- The Fights bench's page object shows three controls to `step_state` (the rest is in methods), so TT4's answer comes
  from `step_screenshot`'s tree, or TT4 moves to a page whose object exposes its values.
- `run_scenario`'s `sinceGreen.first` showed "…" as mojibake (`â€¦`); `why` showed it right. To look at after the round.

## Step 2: the tasks

- TT1-TT4 in `tasks/`, base `de88f536d` (Tomorrow `game`); `check_task.py` passed each: the seeded copy fails as
  `task.json` records, scores unsolved unfixed and solved with `fix.patch` (TT4: with `answer.txt`).
- `prompt-tomorrow.md` is the template (named by `task.json`'s `prompt`); it says the tests make their own databases.
- `check_task.py` fills an empty `expect.json` pass list from the seeded run (fails and passes, less `notOnHead`).
- Changed from the plan: TT2's data cause could not be seeded (a seed change needs a migration, and EF 10 refuses to
  migrate with pending model changes, which would fail every scenario loudly), so TT2 became a rule with no green run.
  TT3 left the Jobs page: PLT.18.1 fails on bUnit (its input pop-up needs a browser) and, in the browser under the
  runner, on a job list that changes under it.
- Bruinet fixes found on the way, in the base: `step_screenshot` on bUnit gave a `.png` path never written (bUnit
  writes `.html`), and now inlines the markup's visible text; `WebAppHost.FindBuiltApp` looked above the runner's temp
  copy, so no browser-head scenario ran under `run_scenario` (fixed after the base; the tasks run on bUnit).
- Disk: a check copy is about 2.5 GB, and four at once left 9 GB free; run at most three at a time.
