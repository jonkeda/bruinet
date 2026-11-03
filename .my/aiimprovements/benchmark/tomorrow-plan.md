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

| Id | Kind | Symptom (failing scenario) | Proposed cause | Tools it is for |
| --- | --- | --- | --- | --- |
| TT1 | rule deep below a page | PLT.35.1: a thug's brain step takes another move than "close in" | a weight in the fight brain's scoring (`Tomorrow.Game`, Fights), two calls below the bench | `get_trace`, `coverage`, `where_is` |
| TT2 | data, not code | PLT.30.1: 56 items show, not 57 | an item book row in the seed (`SeedSetup` or its JSON) that the loader now skips, from a field renamed in one place | `coverage`, `get_trace` |
| TT3 | regression in a bigger change | PLT.18.1: a lane shows a wrong cap | renames in five files of the studio's Jobs page and lanes, with one cap read from the wrong setting | `why` (`sinceGreen`) |
| TT4 | look at a screen | (none fails) "On the Fights bench, walker against thug*2, seed 17: what chance does the attack on thug 1 show, and what does thug's first brain step take?" | none: the answer is only on the screen, computed by the rules | `step_start`, `step_state`, `step_screenshot` |

TT3's copy gets a green run first, as round 1's regressions did. TT4 is scored on its answer only; the copy must
not change.

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
| 1 | done | (this commit) | see "Step 1" below |

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
