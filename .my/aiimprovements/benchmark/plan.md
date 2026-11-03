# Benchmark: do AI1-AI11 help an agent?

Status: **plan**, 2026-11-03. Steps run one by one; each ends with a stop for the owner.

## Question

Does an agent with the MCP tools AI1-AI11 added solve test-and-fix tasks more often, or more cheaply, than one with
the tools from before AI1? And which items add the most?

The per-item agent checks (`checks/AI*.md`) cannot answer it:

- One run per side: agents vary a lot from run to run.
- Easy bugs: in the Todo sample a grep finds most causes.
- AI9's fixture was used to tune `why`, so its pass is not evidence.
- The counts are the agents' own, not logged.

## Setups

| Setup | Tools | How |
| --- | --- | --- |
| `before` | the tools before AI1: the list, validate, scraper, recording and draft tools | bridge profile: hides the tools below and strips the fields below |
| `all` | every tool on `move` | bridge profile: nothing hidden |
| `without-<item>` | `all` but one item | bridge profile: hides that item's tools and fields |

Tools and fields per item (what `before` hides):

| Item | Tools | Fields in other answers |
| --- | --- | --- |
| AI1 | `run_scenario`, `last_runs` | |
| AI2, AI2c | `get_failure`, `failing` | `failure` in `run_scenario` |
| AI2b | `coverage` | |
| AI3 | `tests_affected` | |
| AI4, AI4b | `step_start`, `step_move`, `step_screenshot`, `step_state` | |
| AI5 | `rename_step`, `rename_page_object` | |
| AI6 | `step_log` | `appEvents` |
| AI7 | `where_is` | `appSource` |
| AI8 | `get_trace` | |
| AI9 | `why` | `sinceGreen` |
| AI10 | `list_states`, `set_state` | |
| AI11 | `http_calls` | `replay` argument |

In `before` the agent runs tests with `dotnet test --filter` from the shell, as an agent did before AI1. The server's
instructions are cut to the sentences about the tools that remain.

## Tasks

A task is a folder in `benchmark/tasks/<id>/`:

| File | What |
| --- | --- |
| `task.md` | the prompt (the symptom only, names no tool), the app, the suite, the scenario |
| `seed.patch` | the change that makes it fail, applied on top of the base |
| `base.md` | the base commit of `move`, and whether the copy gets a green run first (for regressions) |
| `expect.json` | success: the scenarios that must pass, the files that must change (any of), the files that must not change |

The first eight, four of them held out (never used while changing a tool):

| Id | Kind | The cause | Suite | Tools it is for | Held out |
| --- | --- | --- | --- | --- | --- |
| T1 | regression after changes | confirm buttons swapped, four comment-only edits | Todo MAUI | AI9 | no |
| T2 | locator after a UI change | a test id renamed in the web app's razor | Todo web | AI7, AI2 | no |
| T3 | backend answer | the sync client drops a field the stub sends | Todo MAUI | AI11, AI6 | no |
| T4 | look at a screen | "what does the list show with the three-todos test set" | Todo MAUI | AI10, AI4 | no |
| T5 | timing | a stub delay longer than a wait's budget | Todo web | AI8 | yes |
| T6 | exception | a view model throws on an empty title; the step only times out | Todo MAUI | AI6 | yes |
| T7 | regression in a bigger change | a service refactor across eight files, one of them wrong | Todo MAUI | AI9 | yes |
| T8 | write a scenario | a new scenario for an existing feature, from existing phrases | Todo web | AI1, AI3 | yes |

Later tasks add Tomorrow itself, where a grep does not find the cause.

## Runs

- One fresh general-purpose agent per run, on the session's default model; the same prompt template for every
  setup, naming no tool.
- The copy is a `git archive` of the base with no `.my`, `.claude` or skills, plus one commit of its own (for `why`).
  The scorer uses git; the agent may not.
- Limits: 60 minutes and 40 tool calls per run; a run over either fails.
- Repeats: 3 per task per setup in the main round.
- Order: setups alternate per task, so the machine's load hits both alike.

## Measures

Logged by the bridge or the scorer, never asked of the agent:

| Measure | From |
| --- | --- |
| Solved | the scorer: `expect.json`'s scenarios pass on a clean build, a listed file changed, no forbidden file changed |
| Tool calls, by name, with milliseconds and answer size | the bridge's log (`MCP_LOG`) |
| Shell commands and file reads | the agent's transcript |
| Tokens | the agent's usage in its notification |
| Wall time | start to end of the run |

A setup is better when, over the main round, it solves more tasks, or solves as many with a median of 30% fewer tool
calls or tokens, and the gap holds in at least two of three repeats. One run's numbers alone decide nothing.

## Steps

Each step ends with its output committed and a stop for the owner.

| Step | Output | Done when |
| --- | --- | --- |
| 1 | Bridge logging: `MCP_LOG=<file>` writes one JSON line per call (tool, arguments' size, ms, error, answer size) | a run's log has every call it made |
| 2 | Bridge profiles: `MCP_PROFILE=before|all|without-<item>` from the table above | `before` lists none of the AI tools and strips their fields; `all` changes nothing |
| 3 | `benchmark/make_copy.py <task> <folder>`: copy, commit, build, green run when `base.md` asks, seed | T1's copy fails as `task.md` says |
| 4 | `benchmark/score.py <task> <folder> <log>`: solved, counts, a JSON line in `results.jsonl` | T1 scored right for a fixed copy and for an unfixed one |
| 5 | The eight tasks, each with its patch, prompt and `expect.json`, each checked to fail as described | every task's copy fails as described, and its known fix passes |
| 6 | Pilot: T1-T4 × `before`, `all` × 1 run | the harness runs end to end; fixes to it only, not to the tools |
| 7 | Main round: T1-T8 × `before`, `all` × 3 runs (48 runs) | `results.jsonl` complete |
| 8 | Report: `benchmark/report.md`, solved and costs per setup, held-out and tuning tasks apart, which tools were called | the owner reads it |
| 9 | Next, from the report: `without-<item>` runs for the items that look strongest or weakest; Tomorrow tasks; logging real sessions | decided with the owner |

Cost: the main round is 48 runs at about 80k tokens each, about 4M tokens; the pilot about 0.7M.

## Rules

- No tool changes during steps 6-8. A tool fix found there is noted for after the report; a held-out task never
  drives one.
- A task found broken (it fails in both setups for a reason outside the app, or passes unseeded) is fixed in step 5's
  way and all its runs are run again.
- Results are reported as they are, failures included.
