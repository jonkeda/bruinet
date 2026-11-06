# Benchmark report: do AI1-AI11 help an agent?

2026-11-03. 48 scored runs of eight seeded tasks on the Todo sample, each run three times without the AI tools
(`before`) and with them (`all`). The plan, the tasks and every number are beside this file (`plan.md`, `tasks/`,
`results.jsonl`).

## Outcome

The tools make an agent a little cheaper and no more successful on these tasks. With them, an agent used fewer tokens
in 23 of 24 paired runs, about 8% fewer (median 74.7k against 81.1k). It solved 22 of 24 tasks against 24 of 24
without them. By the plan's own bar (a 30% saving, or more solved), neither setup is better.

## Results

| | `before` | `all` |
| --- | --- | --- |
| Solved | 24 of 24 | 22 of 24 |
| Median tokens | 81,082 | 74,728 |
| Median seconds | 95 | 86 |

- The held-out tasks (T5-T8, never used to change a tool) behave like the others: 12 of 12 solved both ways, and 80.2k
  against 73.9k tokens. So the saving is not from tuning.
- Every task got cheaper with the tools, by 2 to 25%. The one pair that went the other way is T5's first, by 8%.
- The biggest saving is T4, looking at a screen: 106k against 84k tokens and half the time. It is also where both
  misses are.
- Time is mixed: `all` was faster on four tasks and slower on four.

## Which tools mattered

- `run_scenario` carried most of it: 43 calls, in every `all` run but the T4 ones. Running a scenario and getting
  its failure in one answer is what replaced building and reading `dotnet test` output.
- `why` was used in every T1 and T7 run, the two regressions, and pointed at the right file each time.
- The state tools (`set_state`, `step_state`, `step_move`) were used in every T4 run. They saved tokens, but the
  answer was wrong twice.
- Never called: `get_failure`, `get_trace`, `where_is`, `coverage`, `tests_affected`, `step_log` and `http_calls`.
  Their answers either arrived inside `run_scenario`'s result, or the task never needed them.

## What to fix

- `set_state` pauses before the app's start-up sync, so the screen it shows is not the one a user sees. Both misses
  came from this. It should wait for the app to settle, or say that it has not.
- `set_state` refuses a test set by name (`testData: "three-todos"`) that `list_test_sets` lists. Every T4 run lost
  calls to it.
- `step_screenshot` writes outside the solution's folders, where an agent may not read. It should write beside the
  copy, or return its tree inline.
- `step_state` gives no rows of a list, so every T4 run took the titles from the test data, not from the screen.

## How far this goes

- The Todo sample is small. Reading the code and `dotnet test` found every cause, so tools that explain a run had
  little room to help. A larger app, such as Tomorrow, is the test this one cannot be.
- Each task had one obvious first move. Tasks whose cause hides in timing, data or a long history would test
  `get_trace`, `why` and `step_log` harder.
- 48 runs cost about 3.8M tokens. One batch first ran without copies and was run again; its void runs are not
  counted.

## After the fixes

The four faults are fixed (commit `8563ab84`) and T4 was run again as `T4b`, three times each way
(`rerun-results.jsonl`). Both setups solved all six. With the tools the agent used 75.4k tokens against 106.3k (29%
fewer) and 36 seconds against 195 (median).

- `set_state` now waits for the start-up sync, so every answer had "Synced 10:00".
- Without the tools, agents built their own probes (a feature file, a console app) and took up to six minutes.
- One run first left `{testSet}` in the state; the new refusal said why, and the next call worked.
- Right after `set_state`, `step_state` once answered no page, as none was current; the agent fell back to
  `step_screenshot`'s tree. Fixed since: with no page current, `step_state` reads the one page that says it is loaded.

## Round 2: Tomorrow

2026-11-03 to 11-06. 24 scored runs of four seeded tasks on Tomorrow's studio suite, three times each way, then 12 runs
with the unused tools hidden. The plan and every number are in `tomorrow-plan.md`.

On a real app the tools save more, and it is one kind of task that gains. With them an agent used 13% fewer tokens and
a third less time, in 11 of 12 pairs, and the gap held in all three repeats. Both setups found every cause. By the
plan's bar only looking at a screen is a clear win: the rest saved about 7%, or nothing.

| | `before` | `all` |
| --- | --- | --- |
| Solved | 12 of 12 (11 inside 40 calls) | 12 of 12 |
| Median tokens | 96.2k | 83.4k |
| Median seconds | 175 | 120 |

| Task | Tokens, `before` | Tokens, `all` |
| --- | --- | --- |
| TT1, a rule deep in the fight code, after a tidy-up | 101.3k | 103.9k |
| TT2, a rule below a page, no earlier green run | 89.3k | 82.9k |
| TT3, a regression among renames in five files | 87.9k | 81.6k |
| TT4, what a screen shows | 122.3k | 83.9k |

- TT4 is the win: 31% fewer tokens, and 63 seconds against 274. Without the tools every agent wrote a scenario that
  fails on purpose, to get the page saved. One of those runs made 46 tool calls, over the limit of 40, the only run in
  either round to pass it.
- Without TT4 the saving is 8% in tokens and 11% in time, much as in round 1. So the larger app did not change what the
  tools do for fixing a fault; the tasks did not hide the cause well enough to tell.
- TT1 saved nothing. `why` named the four changed files, and the agents then read them all. It says what changed, not
  which change matters.
- `why`, `run_scenario` and the step tools carried the work again.

## Which tools to keep

Six tools were called in none of the 36 main-round runs that had them (24 in round 1, 12 in round 2). Hiding them for 12 more runs lost nothing: all 12
solved, and no agent asked for one. It saved nothing that shows either: 85.6k tokens against 83.4k, inside the spread.
Those runs were about 45% slower on every task, but three days later on a machine in other use, so that is not read as
their doing.

So `get_failure`, `where_is` and `step_log` are gone, for `run_scenario` and `get_trace` give the same. `coverage`,
`tests_affected` and `http_calls` stay, out of the server's instructions, for they answer questions these tasks never
asked. The cost is that nothing now says where a control is made unless a failure names it.

The larger find came from the logs: about half of all `why` calls failed, because the tools took only a scenario's
exact heading line and agents pass the line a failure gives them. They now take any line of the scenario. No run has
measured that yet.

## What round 2 cannot say

- The tasks stayed easy. Every cause was one change, two unit tests failed on TT1's, and a comment states the rule that
  TT2's and TT3's break.
- Only the studio suite ran, on bUnit. Browser runs did not work under the runner until step 2's fix, and the Jobs
  page's scenario still does not pass there.
- Three runs a task cannot show a small difference. The lean round's time was not measured side by side.
- Round 2 cost about 4M tokens: pilot 0.7M, main round 2.3M, lean round 1.1M.

## Next

Log what agents call in real sessions on Tomorrow, the one thing no seeded task can show, and read it after a few weeks.
Before that, three small Bruinet faults from these runs: `run_scenario` runs `@browser` scenarios on bUnit, where they
fail; `step_screenshot`'s text holds Blazor's hidden error banner; and the runners' temp copies grew to 42 GB.
