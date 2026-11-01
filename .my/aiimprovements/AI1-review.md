# AI1 review: run_scenario and last_runs (with AI3 and AI5)

Status: **done**, 2026-11-01. Commits 24057842 (AI1), 2348bab4 (AI3, AI5), and the review fixes after them.

## What was built

- `run_scenario`, `last_runs` (AI1), `tests_affected` (AI3), `rename_step`, `rename_page_object` (AI5) on the MCP server.
- `BddLanguageServer.RunToEndAsync`: the preview's Run, awaitable, always ended (completed in a `finally`).
- The runner host records a scenario at its first failed step, as the gate does (a Run that failed early kept no result).
- The runner's error names the type of an exception whose message is a bare name.
- Todo's SQL replay finds its recordings from its assembly's build folder (AD-025), so a host's runner works on it.

## Checks

| Check | Result |
| --- | --- |
| `Bruinet.Cli.Tests` | 354 passed, 8 failed: the Generator integration tests (inputs read three folders above the binary) and DataValidator app tests (test sets found from the binary's folder); both read paths relative to the normal `bin` layout, which the run's separate build folder (`TestResults/ai-build`, as `tools/Bruinet.Cli/bin` was locked by 19 running `bruinet` processes) breaks. None is in changed code; a run at the base commit could not build in a fresh worktree (the CLI copies Narrator.Windows from the normal `bin`). |
| `Bruinet.Uat.Tests` | 233 + the new runner test, passed |
| End to end | TOD.05 on MauiHeadless through `run_scenario`: 4/4 passed; with the seeded bug, TOD.05.3 failed at line 46 with "Expected Text to be 'Done' ... Actual: 'Overdue'" |
| Agent check | inconclusive, see `checks/AI1.md` |

## Review findings and what was done

| Finding | Done |
| --- | --- |
| Bad arguments (`timeoutMinutes` 0, negative, a string) ended the whole MCP server | arguments validated (`Whole`), and any fault in a tool is the tool's error answer, never the server's end; test |
| The wait could hang when the runner would not close | `Ended` completed in a `finally`; `RunToEndAsync` turns a fault into an "error" end |
| The timeout did not cover starting the runner | the token reaches `Warm.TakeAsync` |
| No address or duration in a result | `RunScenarioResult` carries both; test |
| "timed out" reported after a run that ended by itself | only when the run ended canceled by the timeout |
| Messages uncapped in the run's end and `last_runs` | capped at 2000 characters |
| Every runner error got a type prefix | only a one-word message gets it; CHANGELOG line |
| CHANGELOG overstated the old behaviour | reworded under Changed: a Run or Step-through records at the first failed step |
| No direct test of recording at a failed step | `AScenarioLeftAtAFailedStep_WritesItsResult_AsTheGateDoes_AndGoingOnWritesNoOther` |
| `_recorded` checked after building a result | checked first |
| Done-when said TOD.02 on web and bUnit; run was TOD.05 on MauiHeadless | kept: the owner's scope is Html and MAUI first and the agent loop runs headless; the head is the server's `BRUINET_HEAD`, a `head` argument would need a warm runner per head (left for AI4, which adds per-session state) |
| One run blocks the server; `notifications/cancelled` is dropped | recorded, not done: `record_video` works the same; AI4's step session is where a long run becomes interactive |
| Plan and skill changes in the AI1 commit | noted; later items commit their plan edits with their close |

## Decisions

- Built in `TestResults/ai-build` (`--artifacts-path`), not `tools/Bruinet.Cli/bin`: other sessions' `bruinet` processes hold it.
- `rename_page_object` asks first (`confirm`), like the Scraper's other writing tools; it changes only the declaring files and lists the others.
- AI3 and AI5 had no agent check of their own: thin wrappers over tested commands, proven by contract tests; AI2's fixture exercises them.
