# AI4 review: the Step-through on the MCP server

Status: **partly done**, 2026-11-02. Commit dfeaef23 and the review fixes after it.

| Finding | Done |
| --- | --- |
| Blocker: after a Waiting answer the agent could not go on; a later pause was lost | the server keeps the last step event; `step_move wait` gives one raised meanwhile, or waits for the next |
| A wait of up to 120 minutes held the whole server | 1 to 10 minutes, 2 by default |
| Disposing the server left a waiter hanging | a dispose raises an end ("stopped") |
| A stop could answer with a pause raised as it came | a stop waits for the end; a pause after a stop is not raised |
| step_screenshot hid the runner's reason | its reason is the answer |
| Departures from the plan unrecorded | recorded in the plan's status row |
| Nulls and an uncapped table in every answer; no depth | only fields with a value; arguments capped at 300 each; depth given |
| `continue` stops at the IDE's breakpoints | said in the tool's description |
| Tests: only the happy path | stop, screenshot and a failed start added; a Waiting test needs a slow step in the counter fixture, not done |
