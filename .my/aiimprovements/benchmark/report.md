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
- Still open: right after `set_state`, `step_state` can answer an empty page before a page is current; the agent fell
  back to `step_screenshot`'s tree.

## Next

Make `step_state` wait for the current page after `set_state`. Then a round on Tomorrow
with three or four tasks the source does not give away, and `without-AI9` and `without-AI10` runs to price those
two items on their own.
